using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTranslator.Model
{
    public class Git
    {
        public string url;
        public string assets_url;
        public string upload_url;
        public string html_url;
        public int id;
        public string node_id;
        public string tag_name;
        public string target_commitish;
        public string name;
        public bool draft;
        public bool prerelease;
        public DateTime created_at;
        public DateTime published_at;
        public List<Asset> assets;
        public string tarball_url;
        public string zipball_url;
        public string body;
    }

    public class Asset
    {
        public string url;
        public int id;
        public string node_id;
        public string name;
        public object label;
        //public Uploader uploader;
        public string content_type;
        public string state;
        public int size;
        public int download_count;
        public DateTime created_at;
        public DateTime updated_at;
        public string browser_download_url;
    }
}
