using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


/* Written in accordance to www.http://id3.org/id3v2.4.0-structure */
namespace ID3Library
{
    public class ID3Frame
    {
        public int size;
        public char[] frameId; // should be 4 bytes
        public byte[] flags; // should be 2 bytes
        public string data;
    }
    public class ID3Header
    { // total header should be 10 bytes
        public int size;
        public char[] tag; // 3 bytes
        public char majorVer;
        public char minorVer;
        public byte flags;
        public int start;
        public int framesStart; // frames start gives the byte position where frames start. This is to skip the header and any extended header if present
    }
    public class ID3Tag
    {
        public List<ID3Frame> frames;
        public ID3Header head;
        // Constructor, must new the List to allocate. Otherwise Null exceptions!
        public ID3Tag()
        {
            frames = new List<ID3Frame>();
        }
    }
    public class ID3Info
    {
        public string title;
        public string artist;
        public string album;
        public string length;
        public string year;
    }

    public class ID3Lib
    {
        public string filename;
        private FileStream fs;
        private byte[] data;

        public ID3Lib()
        {

        }
        // Scan broken into public accessors with overloads, real functionality in the ScanFunc() function
        public ID3Tag Scan()
        {
            return ScanFunc();
        }
        // Overload to allow filename to be set with single function call 
        public ID3Tag Scan(string _filename)
        {
            filename = _filename;
            return ScanFunc();
        }

        private ID3Tag ScanFunc()
        {
            ID3Tag tag = new ID3Tag();
            Load();
            tag.head = FindHeader();
            int byteIndex = tag.head.framesStart; // header is 10 bytes + extended header if present 
            while (byteIndex < (tag.head.size - tag.head.framesStart))
            {
                tag.frames.Add( ParseFrame(byteIndex, out byteIndex));
            }
            return tag;
        }

        // Create an overload to assign filename with Load
        private void Load()
        {
            using( fs = File.Open(filename, FileMode.Open) )
            {
                data = new byte[fs.Length];
                while (fs.Read(data, 0, Convert.ToInt32(fs.Length)) > 0) { }
            }
        }

        private ID3Header FindHeader()
        {
            ID3Header header = new ID3Header();
            if (data == null) return null;
            byte[] id3 = new byte[] {0x49,0x44,0x33};
            byte[] test;

            for (int i = 0; i < (data.Length-10); i++)
            {
                test = new byte[] {data[i], data[i+1], data[i+2]};
                // Check for 3 bytes that = ID3 
                if (  test.SequenceEqual(id3) ) // sequenceequal does an array byte compare  // new byte[] {data[i], data[i+1], data[i+2]}) 
                {
                    header.start = i; // starting position of the header in the byte array 
                    header.tag = (new char[] { Convert.ToChar(data[i]), Convert.ToChar(data[i + 1]), Convert.ToChar(data[i + 2]) });
                    header.majorVer = Convert.ToChar(data[i + 3]);
                    header.minorVer = Convert.ToChar(data[i + 4]);
                    header.flags = data[i + 5];
                    byte[] sizeBytes = new byte[] { data[i + 6], data[i + 7], data[i + 8], data[i + 9] };
                    header.size = ConvertSyncSafeToInt(sizeBytes);
                    header.framesStart = i + 10;
                    if ((header.flags & 0x80) == 0x80)
                    {
                        /// Unsynchronisation is used!
                    }
                    if ((header.flags & 0x40) == 0x40)
                    {
                        // Extended header is present, need to read this... 
                        //Extended header size   4 * %0xxxxxxx
                        //Number of flag bytes       $01
                        //Extended Flags             $xx
                        sizeBytes = new byte[] { data[i + 10], data[i + 11], data[i + 12], data[i + 13] };
                        int extHeaderSize = ConvertSyncSafeToInt(sizeBytes);
                        header.framesStart = i + 10 + extHeaderSize;

                    }
                    if ((header.flags & 0x10) == 0x10)
                    {
                        // Footer present
                    }
                    return header;
                }
            }
            return header;
        }

        // Size values stored as syncsafe values. 28 bits stored in 32bits by removing the MSB of each byte. 
        // For the purposes of ID3 tags this is always 4 bytes so do the conversion on 4 bytes statically. 
        private int ConvertSyncSafeToInt(byte[] b)
        {
            int val=0;
            // foreach is not mutable. 
            // need to and with 0x7F just in the off chance that the MSB is not zero like it should be. 
            try
            {
                val = ((b[0] & 0x7F) << 21 | (b[1] & 0x7F) << 14 | (b[2] & 0x7F) << 7 | (b[3] & 0x7F));
            }
            catch (Exception ex)
            {
                // If the byte array isn't enough elements or something .... 
                // throw a custom exception here
            }
            return val;
        }

        private ID3Frame ParseFrame(int startIndex, out int stopIndex)
        {
            ID3Frame frame = new ID3Frame();
            int i = startIndex;
            frame.frameId = new char[] { Convert.ToChar(data[i]), 
                Convert.ToChar(data[i + 1]), 
                Convert.ToChar(data[i + 2]), 
                Convert.ToChar(data[i + 3]) };
            i = startIndex + 4;
            byte[] sizeBytes = new byte[] { data[i], data[i + 1], data[i + 2], data[i + 3] };
            // frame size is NOT syncsafe so don't convert or sizes will be wrong
            frame.size = (sizeBytes[0] << 24 | sizeBytes[1] << 16 | sizeBytes[2] << 8 | sizeBytes[3]);
            i = startIndex + 8;
            frame.flags = new byte[] { data[i], data[i + 1] };
            if ((frame.flags[0] != 0x0) | (frame.flags[1] != 0x0))
            {
                int flagisset = 1;
            }
            i = startIndex + 10;
            byte[] frameData = new byte[frame.size];
            Buffer.BlockCopy(data, i, frameData, 0, frame.size); // Copy the block of frame data
            frame.data = System.Text.Encoding.Default.GetString(frameData); //  frameData.ToString();
            stopIndex = startIndex + frame.size + 10; // size does not include the 10 byte header so tack that on the end
            return frame;
        }

        public ID3Info ParseTag(ID3Tag tag)
        {
            ID3Info info = new ID3Info();
            foreach (ID3Frame frame in tag.frames)
            {
                if (new string(frame.frameId) == "TIT2") info.title = frame.data.Replace("\0", string.Empty);
                if (new string(frame.frameId) == "TIT1") info.artist = frame.data.Replace("\0", string.Empty);
                if (new string(frame.frameId) == "TALB") info.album = frame.data.Replace("\0", string.Empty);
                if (new string(frame.frameId) == "TLEN") info.length = frame.data.Replace("\0", string.Empty);
                if (new string(frame.frameId) == "TYER") info.year = frame.data.Replace("\0", string.Empty);
            }
            return info;
        }
            
    }
}
