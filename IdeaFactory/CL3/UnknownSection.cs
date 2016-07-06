﻿// 
// This file is licensed under the terms of the Simple Non Code License (SNCL) 2.0.2.
// The full license text can be found in the file named License.txt.
// Written originally by Alexandre Quoniou in 2016.
//

using System.Diagnostics.Contracts;

namespace MysteryDash.FileFormats.IdeaFactory.CL3
{
    public class UnknownSection : Section
    {
        public byte[] Data { get; set; }
        public int Count { get; set; }

        public UnknownSection(byte[] nameBytes, byte[] data, int count)
        {
            NameBytes = nameBytes;
            Data = data;
            Count = count;
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(Data != null);
            Contract.Invariant(Count >= 0);
        }
    }
}