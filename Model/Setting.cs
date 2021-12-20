using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTranslator.Model
{
    [ProtoContract]
    internal class Setting
    {
        public void Init()
        {
            this.IsJavi = true;
            this.IsAutoTranslate = true;
            this.IsFaded = 2;
            this.IsLoadKanji = true;
            this.IsRunOnStartUp = true;
            this.IsDoubleClickOn = true;
        }

        [ProtoMember(1)]
        public bool IsJavi { get; set; }
        [ProtoMember(2)]
        public bool IsAutoTranslate { get; set; }
        [ProtoMember(3)]
        public int IsFaded { get; set; }
        [ProtoMember(4)]
        public bool IsLoadKanji { get; set; }
        [ProtoMember(5)]
        public bool IsRunOnStartUp { get; set; }
        [ProtoMember(6)]
        public bool IsDoubleClickOn { get; set; }
        

    }
}
