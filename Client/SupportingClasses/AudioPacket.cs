using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Pocket
{
    public class AudioPacket
    {
        public byte[] Data { get; set; }
        public long Timestamp { get; set; }
        public uint SequenceNumber { get; set; }
        public IPEndPoint SenderEndpoint { get; set; }

        public byte[] Serialize()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(Timestamp);
            writer.Write(SequenceNumber);
            writer.Write(Data.Length);
            writer.Write(Data);

            return ms.ToArray();
        }

        public static AudioPacket Deserialize(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var reader = new BinaryReader(ms);

            return new AudioPacket
            {
                Timestamp = reader.ReadInt64(),
                SequenceNumber = reader.ReadUInt32(),
                Data = reader.ReadBytes(reader.ReadInt32())
            };
        }
    }
}
