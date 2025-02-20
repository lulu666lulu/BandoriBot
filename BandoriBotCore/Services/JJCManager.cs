using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BandoriBot.Services
{
    public sealed class JJCManager : IDisposable
    {
        public static JJCManager Instance = new JJCManager(Path.Combine("jjc"));

#pragma warning disable CS0649
        class CommentModel
        {
            public string parent;
            public string id;
            public DateTime date;
            public string msg, nickname;
            public int avatar;

            public CommentModel(Result parent, Comment comment)
            {
                id = comment.id;
                date = comment.date;
                msg = comment.msg;
                nickname = comment.nickname;
                avatar = comment.avatar;
                this.parent = parent.id;
            }
        }

        class Character
        {
            public bool equip;
            public int id, star;

            public override string ToString()
            {
                return $"{id}-{star}-{equip}";
            }
        }

        class Comment
        {
            public string id;
            public DateTime date;
            public string msg, nickname;
            public int avatar;
        }

        class Result
        {
            public string id;
            public Character[] atk, def;
            public int up, down;
            public Comment[] comment;
            public DateTime updated;
        }
        class RespData
        {
            public Result[] result;
            public PageInfo page;
        }
        class PageInfo
        {
            public bool hasMore;
            public int page;
        }

        private readonly Dictionary<string, Image> textures;
        private readonly Dictionary<string, int> nicknames;
        private readonly Font font;

        private HttpClient client;

        public JJCManager(string root)
        {
            textures = new Dictionary<string, Image>();
            nicknames = new Dictionary<string, int>();
            font = new Font(FontFamily.GenericMonospace, 15);

            foreach (var file in Directory.GetFiles(root))
                if (file.EndsWith(".png"))
                    textures.Add(Path.GetFileNameWithoutExtension(file), Image.FromFile(file));

            foreach (var line in File.ReadAllText(Path.Combine(root, "nickname.csv")).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var splits = line.Split(',');
                if (!int.TryParse(splits[0], out var res)) continue;
                var id = 100 * res + 1;// characters.SingleOrDefault(c => c.name.EndsWith(splits[2]));
                foreach (var nickname in splits.Skip(2))
                    if (!nicknames.ContainsKey(nickname))
                        nicknames.Add(nickname, id);
            }

            client = new HttpClient();

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.88 Safari/537.36 Edg/87.0.664.66");
            client.DefaultRequestHeaders.Add("Referer", "https://pcrdfans.com/");
            client.DefaultRequestHeaders.Add("Origin", "https://pcrdfans.com");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-site");
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            //client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
            client.DefaultRequestHeaders.Remove("Expect");
        }

        private Image GetTexture(Character c)
        {
            Image img;
            if (c.star == 6 && textures.TryGetValue((c.id + 60).ToString(), out img))
                return img;
            else if (c.star >= 3 && textures.TryGetValue((c.id + 30).ToString(), out img))
                return img;
            else
                return textures[(c.id + 10).ToString()];
        }

        private void DrawToGraphics(Graphics canvas, Character c, int offx, int offy)
        {
            canvas.DrawImage(GetTexture(c), new Rectangle(offx, offy, 100, 100));
            canvas.DrawImage(textures["search-li"], new Rectangle(offx, offy, 100, 100));
            for (int i = 0; i < c.star; ++i)
                canvas.DrawImage(textures["star"], new Rectangle(5 + 12 * i + offx, 80 + offy, 15, 15));
            if (c.equip) canvas.DrawImage(textures["arms"], new Rectangle(15 + offx, 15 + offy, 15, 15));
        }

        private void DrawToGraphics(Graphics canvas, Result t, int offx, int offy)
        {
            for (int i = 0; i < 5; ++i)
            {
                DrawToGraphics(canvas, t.atk[i], 110 * i + offx, offy);
                DrawToGraphics(canvas, t.def[i], 110 * i + 590 + offx, offy);
            }
            canvas.DrawString($"顶：{t.up} 踩：{t.down} ", font, Brushes.Black, offx, offy + 100);
        }
        
        private Image GetImage(Result[] teams)
        {
            var n = teams.Length;
            var result = new Bitmap(1130, 120 * n + 20);
            var canvas = Graphics.FromImage(result);
            canvas.Clear(Color.White);
            for (int i = 0; i < n; ++i) DrawToGraphics(canvas, teams[i], 0, i * 120);
            canvas.DrawString($"powered by www.pcrdfans.com", font, Brushes.Black, 0, 120 * n);
            canvas.Dispose();
            return result;
        }

        private static string GenNonce()
        {
            const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
            var rand = new Random();

            return new string(Enumerable.Range(0, 16).Select(_ => chars[rand.Next(36)]).ToArray());
        }
        private static long GetTimeStamp()
        {
            TimeSpan ts = DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return (long)ts.TotalSeconds;
        }

        private delegate void CallbackD(string text);

        [DllImport("PCRDwasm.dll", EntryPoint = "getSign", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GetSign(string text, string nonce, CallbackD callback);

        public async Task<string> Callapi(string[] defs)
        {
            int[] ids = new int[5];
            int i = 0;
            try
            {
                for (; i < 5; ++i) ids[i] = nicknames[defs[i]];
            }
            catch (KeyNotFoundException)
            {
                throw new Exception($"未能识别{defs[i]}");
            }
            JObject obj;


            var json = new JObject
            {
                ["def"] = new JArray(ids),
                ["nonce"] = GenNonce(),
                ["page"] = 1,
                ["region"] = 2,
                ["sort"] = 1,
                ["ts"] = GetTimeStamp()
            };

            string sign = null;
            
            GetSign(json.ToString(Formatting.None), json.Value<string>("nonce"),
                s => sign = s);


            json = new JObject
            {
                ["_sign"] = sign,
                ["def"] = json["def"],
                ["nonce"] = json["nonce"],
                ["page"] = json["page"],
                ["region"] = json["region"],
                ["sort"] = json["sort"],
                ["ts"] = json["ts"]
            };


            RespData result;

            var resp = await client.PostAsync($"https://api.pcrdfans.com/x/v1/search",
                new StringContent(json.ToString(Formatting.None)
                    , Encoding.UTF8, "application/json"));

            var raw = JObject.Parse(await resp.Content.ReadAsStringAsync());

            try
            {
                result = raw["data"].ToObject<RespData>();
            }
            catch
            {
                return $"pcrd err:{raw}";
            }

            return Utils.GetImageCode(GetImage(result.result.Take(10).ToArray()).Resize(0.5f));
        }

        public void Dispose()
        {
            foreach (var pair in textures) pair.Value.Dispose();
        }
    }
}
