﻿// 
// This file is licensed under the terms of the Simple Non Code License (SNCL) 2.0.2.
// The full license text can be found in the file named License.txt.
// Written originally by Alexandre Quoniou in 2016.
//

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MysteryDash.FileFormats;
using MysteryDash.FileFormats.IdeaFactory.PAC;
using MysteryDash.FileFormats.IdeaFactory.TID;

namespace MysteryDash.MultiExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Multi Extractor - By MysteryDash");
            Console.WriteLine();
            Console.WriteLine("Currently supported file formats :");
            Console.WriteLine("- CL3");
            Console.WriteLine("- PAC");
            Console.WriteLine("- TID");
            Console.WriteLine();
            
            if (args.Length == 0)
            {
                Console.WriteLine("Please drag and drop one or multiple files over the executable.");
            }
            else
            {
                Parallel.ForEach(args, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, arg =>
                {
                    try
                    {
                        Console.WriteLine($"Processing {arg}...");

                        var file = Reader.LoadFile(arg);
                        if (file == null)
                        {
                            Console.WriteLine($"Unrecognized file format for {arg} !");
                            return;
                        }

                        var archive = file as IArchive;
                        if (archive != null)
                        {
                            var pac = archive as Pac;
                            pac?.Files.ForEach(entry => entry.KeepCompressed = false);

                            archive.WriteFolder(Path.ChangeExtension(arg, ""));
                            archive.Dispose();
                        }
                        
                        var tid = file as Tid;
                        tid?.Bitmap.Save(Path.ChangeExtension(arg, "png"));

                        // Sometimes the garbage collector doesn't do its job by itself at this point, and it is required to keep the memory usage as low as possible when reading .PAC
                        GC.Collect();
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"I/O error with {arg}. Details : {ex.Message}");
                    }
                });

                Console.WriteLine("Extraction done !");
                Console.ReadKey();
            }
        }
    }
}