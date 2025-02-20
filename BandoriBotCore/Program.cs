using BandoriBot.Commands;
using BandoriBot.Config;
using BandoriBot.Handler;
using BandoriBot.Services;
using Mirai_CSharp;
using Mirai_CSharp.Models;
using Native.Csharp.App.Terraria;
using SekaiClient.Datas;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace BandoriBot
{
    class Program
    {
        public static object SekaiFile = new object();

        private static void PluginInitialize(MiraiHttpSession session)
        {
            MessageHandler.session = session;

            Configuration.Register<Activation>();
            Configuration.Register<Delay>();
            Configuration.Register<MessageStatistic>();
            Configuration.Register<ReplyHandler>();
            Configuration.Register<Whitelist>();
            Configuration.Register<Admin>();
            Configuration.Register<Blacklist>();
            Configuration.Register<BlacklistF>();
            Configuration.Register<TitleCooldown>();
            Configuration.Register<PCRConfig>();
            Configuration.Register<R18Allowed>();
            Configuration.Register<NormalAllowed>();
            Configuration.Register<AccountBinding>();
            Configuration.Register<ServerManager>();
            Configuration.Register<TimeConfiguration>();
            Configuration.Register<GlobalConfiguration>();
            Configuration.Register<Antirevoke>();
            Configuration.Register<SetuConfig>();
            Configuration.Register<Save>();
            Configuration.Register<CarTypeConfig>();
            Configuration.Register<SubscribeConfig>();
            //Configuration.Register<PeriodRank>();

            MessageHandler.Register<CarHandler>();
            MessageHandler.Register(Configuration.GetConfig<ReplyHandler>());
            MessageHandler.Register<WhitelistHandler>();
            MessageHandler.Register<RepeatHandler>();
            MessageHandler.Register(Configuration.GetConfig<MessageStatistic>());

            MessageHandler.Register<YCM>();
            MessageHandler.Register<QueryCommand>();
            MessageHandler.Register<ReplyCommand>();
            MessageHandler.Register<FindCommand>();
            MessageHandler.Register<DelayCommand>();
            MessageHandler.Register<AdminCommand>();
            MessageHandler.Register<SekaiCommand>();
            MessageHandler.Register<SekaiPCommand>();
            MessageHandler.Register<WhitelistCommand>();
            MessageHandler.Register<GachaCommand>();
            MessageHandler.Register<GachaListCommand>();
            MessageHandler.Register<Activate>();
            MessageHandler.Register<Deactivate>();
            MessageHandler.Register<BlacklistCommand>();
            MessageHandler.Register<TitleCommand>();
            MessageHandler.Register<PCRRunCommand>();
            MessageHandler.Register<CarTypeCommand>();
            MessageHandler.Register<SekaiLineCommand>();

            MessageHandler.Register<DDCommand>();
            MessageHandler.Register<CDCommand>();
            MessageHandler.Register<CCDCommand>();
            MessageHandler.Register<SLCommand>();
            MessageHandler.Register<SCCommand>();
            MessageHandler.Register<TBCommand>();
            MessageHandler.Register<RCCommand>();
            MessageHandler.Register<CPMCommand>();

            CommandHelper.Register<AdditionalCommands.随机禁言>();
            CommandHelper.Register<AdditionalCommands.泰拉在线>();
            CommandHelper.Register<AdditionalCommands.泰拉资料>();
            CommandHelper.Register<AdditionalCommands.封>();
            CommandHelper.Register<AdditionalCommands.注册>();
            CommandHelper.Register<AdditionalCommands.在线排行>();
            CommandHelper.Register<AdditionalCommands.物品排行>();
            CommandHelper.Register<AdditionalCommands.财富排行>();
            CommandHelper.Register<AdditionalCommands.渔夫排行>();
            CommandHelper.Register<AdditionalCommands.死亡排行>();
            CommandHelper.Register<AdditionalCommands.用户>();
            CommandHelper.Register<AdditionalCommands.解>();
            CommandHelper.Register<AdditionalCommands.重置>();
            CommandHelper.Register<AdditionalCommands.切换>();
            CommandHelper.Register<AdditionalCommands.绑定>();
            CommandHelper.Register<AdditionalCommands.执行>();
            CommandHelper.Register<AdditionalCommands.解绑>();
            CommandHelper.Register<AdditionalCommands.开启前缀检测>();
            CommandHelper.Register<AdditionalCommands.关闭前缀检测>();
            CommandHelper.Register<AdditionalCommands.开启自动清人>();
            CommandHelper.Register<AdditionalCommands.关闭自动清人>();
            CommandHelper.Register<AdditionalCommands.加入黑名单>();
            CommandHelper.Register<AdditionalCommands.移除黑名单>();
            CommandHelper.Register<AdditionalCommands.黑名单列表>();
            CommandHelper.Register<AdditionalCommands.服务器列表>();
            CommandHelper.Register<AdditionalCommands.解ip>();
            CommandHelper.Register<AdditionalCommands.封ip>();
            CommandHelper.Register<AdditionalCommands.saveall>();

            MessageHandler.Register<R18AllowedCommand>();
            MessageHandler.Register<NormalAllowedCommand>();
            MessageHandler.Register<SetuCommand>();
            MessageHandler.Register<ZMCCommand>();
            MessageHandler.Register<AntirevokeCommand>();
            MessageHandler.Register<SubscribeCommand>();

            if (File.Exists("sekai"))
            {
                ScheduleManager.QueueTimed(async () =>
                {
                    var id = MasterData.Instance.events.Last().id;
                    var data = await Utils.GetHttp($"https://bitbucket.org/sekai-world/sekai-event-track/raw/main/event{id}.json");
                    var fn = $"sekai_event{id}.csv";

                    lock (SekaiFile)
                    {
                        if (!File.Exists(fn)) File.WriteAllText(fn, $"time,{string.Join(",", data.Properties().Where(p => p.Name.StartsWith("rank")).Select(p => p.Name))}\n");

                        File.AppendAllText(fn, $"{data["time"]},{string.Join(",", data.Properties().Where(p => p.Name.StartsWith("rank")).Select(p => p.Value.Single()["score"]))}\n");
                    }
                }, 60);
            }

            Configuration.LoadAll();

            foreach (var schedule in Configuration.GetConfig<TimeConfiguration>().t)
            {
                var s = schedule;
                ScheduleManager.QueueTimed(async () =>
                {
                    await session.SendGroupMessageAsync(s.group, await Utils.GetMessageChain(s.message, async p => await session.UploadPictureAsync(UploadTarget.Group, p)));
                }, s.delay);
            }

            GC.Collect();
            MessageHandler.booted = true;
        }

        public static async Task Main(string[] args)
        {
            string authkey = File.ReadAllText("authkey.txt");

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

            var options = args.Length == 0 ?
                new MiraiHttpSessionOptions("bothost", 8080, authkey) :
                new MiraiHttpSessionOptions("localhost", 8080, authkey);

            await using var session = new MiraiHttpSession();

            session.AddPlugin(new MessageHandler());
            
            await session.ConnectAsync(options, long.Parse(args.Length == 0 ? "2025551588" : args[0]));

            PluginInitialize(session);

            Console.WriteLine("connected to server");

            Thread.Sleep(int.MaxValue);

        }

        private static void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            Console.WriteLine(e.Exception);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ExceptionObject);
        }
    }
}
