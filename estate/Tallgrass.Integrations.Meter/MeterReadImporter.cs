using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Tallgrass.Integrations.Meter
{
    public sealed record MeterRead(string SerialNumber, DateTime ReadAt, decimal Kwh, bool Estimated);

    public interface IMeterReadSink
    {
        void Save(IReadOnlyList<MeterRead> reads);
    }

    /// <summary>
    /// Rewritten in 2022 when the AMI vendor changed. Constructor-injected, tested, boring.
    /// It changes every sprint because the vendor file format does.
    /// </summary>
    public sealed class MeterReadImporter
    {
        private readonly IMeterReadSink _sink;
        private readonly TimeProvider _clock;

        public MeterReadImporter(IMeterReadSink sink, TimeProvider clock)
        {
            _sink = sink;
            _clock = clock;
        }

        public int Import(IEnumerable<string> lines)
        {
            var reads = lines.Skip(1)
                .Select(l => l.Split('|'))
                .Where(p => p.Length >= 4)
                .Select(p => new MeterRead(p[0], DateTime.Parse(p[1], CultureInfo.InvariantCulture),
                                           decimal.Parse(p[2], CultureInfo.InvariantCulture), p[3] == "E"))
                .Where(r => r.ReadAt <= _clock.GetUtcNow().UtcDateTime)
                .ToList();
            _sink.Save(reads);
            return reads.Count;
        }
    }
}
