using System;
using System.Collections.Generic;
using System.IO;

namespace Tallgrass.Payments
{
    public class LockboxParser
    {
        public IEnumerable<string[]> Parse(string path)
        {
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("H") || line.StartsWith("T")) continue;   // header / trailer
                yield return new[] { line.Substring(1, 10).Trim(), line.Substring(11, 12).Trim(), line.Substring(23, 8) };
            }
        }
    }
}
