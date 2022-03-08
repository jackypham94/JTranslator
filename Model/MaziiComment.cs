using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTranslator.Model
{
    public class MaziiComment
    {
        public int wordId { get; set; }
        public int status { get; set; }
        public List<Comment> result { get; set; }
    }
    public class Comment
    {
        public int dislike { get; set; }
        public int like { get; set; }
        public string mean { get; set; }
        public int reportId { get; set; }
        public int status { get; set; }
        public int type { get; set; }
        public int userId { get; set; }
        public string username { get; set; }
        public string word { get; set; }
        public string wordId { get; set; }
        public string type_data { get; set; }
        public string dict { get; set; }
        public object action { get; set; }
    }
}
