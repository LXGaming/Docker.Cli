﻿using System;
using LXGaming.Docker.Cli.Commands.Compose;
using LXGaming.Docker.Cli.Commands.Update;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LXGaming.Docker.Cli {

    public static class Program {

        public static int Main(string[] args) {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
            Console.CancelKeyPress += (_, _) => Shutdown();

            var app = new CommandApp();
            app.Configure(config => {
                config.SetApplicationName("LXGaming.Docker.Cli");
                config.SetApplicationVersion("1.0.4");
                config.PropagateExceptions();

                config.AddCommand<ComposeCommand>("compose");
                config.AddCommand<UpdateCommand>("update");
            });

            try {
                return app.Run(args);
            } catch (Exception ex) {
                AnsiConsole.WriteException(ex);
                return 1;
            }
        }

        private static void Shutdown() {
            AnsiConsole.Cursor.Show();
            AnsiConsole.Reset();
        }
    }
}