﻿// 
// This file is licensed under the terms of the Simple Non Code License (SNCL) 2.0.2.
// The full license text can be found in the file named License.txt.
// Written originally by Alexandre Quoniou in 2016.
//

// TODO : Compression Algorithm

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using MysteryDash.FileFormats.IO;

namespace MysteryDash.FileFormats.IdeaFactory.PAC
{
    /// <summary>
    /// Provides access to pac archives.
    /// </summary>
    public class Pac : IFileFormat, IArchive
    {
        private List<Stream> _streams;

        public List<PacEntry> Files { get; private set; }
        public bool Loaded => true;
        public bool LeaveStreamsOpened = false;

        public Pac()
        {
            _streams = new List<Stream>();
            Files = new List<PacEntry>();
        }

        public void LoadFile(string path)
        {
            Contract.Requires<ObjectDisposedException>(Files != null);
            Contract.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(path));
            Contract.Requires<FileNotFoundException>(File.Exists(path));

            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            LoadFromStream(stream);
        }

        public void LoadBytes(byte[] data)
        {
            Contract.Requires<ObjectDisposedException>(Files != null);
            Contract.Requires<ArgumentNullException>(data != null);

            var stream = new MemoryStream(data);
            LoadFromStream(stream);
        }

        public void LoadFromStream(Stream stream)
        {
            Contract.Requires<ObjectDisposedException>(Files != null);
            Contract.Requires<ArgumentNullException>(stream != null);
            Contract.Requires(stream.CanRead);
            Contract.Requires(stream.CanSeek);

            LoadFromStream(stream, false, true);
        }

        public void LoadFromStream(Stream stream, bool cacheFiles, bool keepCompressed)
        {
            Contract.Requires<ObjectDisposedException>(Files != null);
            Contract.Requires<ArgumentNullException>(stream != null);
            Contract.Requires(stream.CanRead);
            Contract.Requires(stream.CanSeek);

            _streams.Add(stream);

            var origin = (int)stream.Position;
            
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                if (new string(reader.ReadChars(8)) != "DW_PACK\0")
                    throw new InvalidDataException("This isn't a PAC file.");

                stream.Seek(0x04, SeekOrigin.Current);
                var fileCount = reader.ReadInt32();
                
                var dataOffset = 0x14 + 0x120 * fileCount;

                for (int i = 0; i < fileCount; i++)
                {
                    stream.Seek(0x14 + 0x120 * i + 0x08 + origin, SeekOrigin.Begin); // Header + Entries already read + Padding + Id
                    var filePath = reader.ReadBytes(0x104);
                    stream.Seek(0x04, SeekOrigin.Current);
                    var size = reader.ReadInt32();
                    var extractedSize = reader.ReadInt32();
                    var isCompressed = reader.ReadInt32() != 0;
                    var relativedDataOffset = reader.ReadInt32(); // Relative offset to beginning of data

                    if (cacheFiles)
                    {
                        stream.Seek(dataOffset + relativedDataOffset + origin, SeekOrigin.Begin);
                        Files.Add(new PacEntry(this, filePath, isCompressed, extractedSize, reader.ReadBytes(size), keepCompressed));
                    }
                    else
                    {
                        Files.Add(new PacEntry(this, filePath, isCompressed, extractedSize, stream, dataOffset + relativedDataOffset + origin, size, false, keepCompressed));
                    }
                }
            }
        }

        public void LoadFolder(string path, bool ignoreFileOnInvalidPath = false)
        {
            Contract.Requires<ObjectDisposedException>(Files != null);

            LoadFolder(path, ignoreFileOnInvalidPath, false, true);
        }


        public void LoadFolder(string path, bool ignoreFileOnInvalidPath, bool preloadFiles, bool keepCompresed)
        {
            Contract.Requires<ObjectDisposedException>(Files != null);

            path = path.TrimEnd('\\') + '\\';
            var pathLength = path.Length;

            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories).ToArray();
            var relativePaths = files.Select(file => Encoding.UTF8.GetBytes(file.Remove(0, pathLength))).ToArray();
            
            for (int i = 0; i < files.Length; i++)
            {
                if (relativePaths[i].Length > 0x104)
                {
                    if (ignoreFileOnInvalidPath)
                        continue;
                    throw new PathTooLongException();
                }

                if (preloadFiles)
                {
                    var file = File.ReadAllBytes(files[i]);
                    Files.Add(new PacEntry(this, relativePaths[i], false, file.Length, file, keepCompresed));
                }
                else
                {
                    var fileStream = File.OpenRead(files[i]);
                    Files.Add(new PacEntry(this, relativePaths[i], false, (int)fileStream.Length, fileStream, 0, (int)fileStream.Length, true, keepCompresed));
                }
            }
        }

        public void WriteFile(string path)
        {
            Contract.Requires<ObjectDisposedException>(Files != null);
            Contract.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(path));

            using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                WriteToStream(stream);
            }
        }

        public byte[] WriteBytes()
        {
            Contract.Requires<ObjectDisposedException>(Files != null);

            using (var stream = new MemoryStream())
            {
                WriteToStream(stream);
                return stream.ToArray();
            }
        }

        public void WriteToStream(Stream stream)
        {
            Contract.Requires<ObjectDisposedException>(Files != null);
            Contract.Requires<ArgumentNullException>(stream != null);
            Contract.Requires(stream.CanWrite);
            Contract.Requires(stream.CanSeek);

            var origin = stream.Position;

            using (var writer = new EndianBinaryWriter(stream))
            {
                writer.Write("DW_PACK\0".ToCharArray());
                stream.Seek(0x04, SeekOrigin.Current);
                writer.Write(Files.Count);

                var dataOffset = 0x14 + 0x120 * Files.Count;
                var relativeDataOffset = 0x00;

                for (int i = 0; i < Files.Count; i++)
                {
                    stream.Seek(0x18 + i * 0x120 + origin, SeekOrigin.Begin); // Skip to next entry
                    writer.Write(i);
                    writer.Write(Files[i].Path.GetCustomLength(0x104));
                    stream.Seek(0x04, SeekOrigin.Current);
                    writer.Write(Files[i].File.Length);
                    writer.Write(Files[i].DecompressedSize);
                    writer.Write(Files[i].KeepCompressed ? 0x01 : 0x00);
                    writer.Write(relativeDataOffset);

                    stream.Seek(dataOffset + relativeDataOffset + origin, SeekOrigin.Begin);
                    writer.Write(Files[i].File);

                    relativeDataOffset += Files[i].File.Length;
                }
            }
        }

        public void WriteFolder(string path)
        {
            Contract.Requires<ObjectDisposedException>(Files != null);
            Contract.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(path));

            foreach (var file in Files)
            {
                var realPath = Path.Combine(path, file.Path.ZeroTerminatedString);
                Directory.CreateDirectory(Path.GetDirectoryName(realPath));
                File.WriteAllBytes(realPath, file.File);
            }
        }

        public bool RemoveFile(string path)
        {
            return Files.RemoveAll(entry => entry.Path.ZeroTerminatedString == path) > 0;
        }

        public void Dispose()
        {
            if (_streams == null)
            {
                throw new ObjectDisposedException(nameof(Pac));
            }

            if (!LeaveStreamsOpened)
            {
                _streams.ForEach(stream => stream.Close());
            }

            _streams = null;
            Files = null;
        }
    }
}
