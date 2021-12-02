using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTranslator.Model
{
    public class Google
    {
        public List<Sentence> sentences { get; set; }
        public int status { get; set; }
    }

    public class Sentence
    {
        public string trans { get; set; }
        public string orig { get; set; }
        public int backend { get; set; }
        public string src_translit { get; set; }
    }
}
