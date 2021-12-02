using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTranslator.Model
{
    public class Entry
    {
        public string definition_id { get; set; }
        public List<string> synonym { get; set; }
    }

    public class Synset
    {
        public List<Entry> entry { get; set; }
        public string base_form { get; set; }
        public string pos { get; set; }
    }

    public class RelatedWords
    {
        public List<string> word { get; set; }
    }

    public class Mean
    {
        public string mean { get; set; }
        //public object examples { get; set; }
        public string kind { get; set; }
    }

    public class Datum
    {
        //public int mobileId { get; set; }
        public string phonetic { get; set; }
        //public string _id { get; set; }
        //public List<Synset> synsets { get; set; }
        //public RelatedWords related_words { get; set; }
        public List<Mean> means { get; set; }
        public List<string> opposite_word { get; set; }
        public string word { get; set; }
    }

    public class Mazii
    {
        public int status { get; set; }
        //public bool found { get; set; }
        public List<Datum> data { get; set; }
    }
}
