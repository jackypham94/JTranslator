using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTranslator.Model
{
    [ProtoContract]
    public class CompDetail
    {
        [ProtoMember(1)]
        public string w { get; set; }
        [ProtoMember(2)]
        public string h { get; set; }
    }
    [ProtoContract]
    public class Example
    {
        [ProtoMember(1)]
        public string w { get; set; }
        [ProtoMember(2)]
        public string p { get; set; }
        [ProtoMember(3)]
        public string m { get; set; }
        [ProtoMember(4)]
        public string h { get; set; }
    }

    [ProtoContract]
    public class Result : IEquatable<Result>
    {
        [ProtoMember(1)]
        public string kanji { get; set; }
        [ProtoMember(2)]
        public string comp { get; set; }
        [ProtoMember(3)]
        public string detail { get; set; }
        [ProtoMember(4)]
        public string level { get; set; }
        public int mobileId { get; set; }
        [ProtoMember(5)]
        public string stroke_count { get; set; }
        public string freq { get; set; }
        [ProtoMember(6)]
        public string on { get; set; }
        [ProtoMember(7)]
        public string mean { get; set; }
        [ProtoMember(8)]
        public List<CompDetail> compDetail { get; set; }
        [ProtoMember(9)]
        public string kun { get; set; }
        [ProtoMember(10)]
        public List<Example> examples { get; set; }
        [ProtoMember(11)]
        public DateTime date { get; set; }

        public bool Equals(Result other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return kanji == other.kanji;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((Result)obj);
        }

        public override int GetHashCode()
        {
            return (kanji != null ? kanji.GetHashCode() : 0);
        }
    }

    public class MaziiKanji
    {
        public int status { get; set; }
        public List<Result> results { get; set; }
        public int total { get; set; }
    }
}
