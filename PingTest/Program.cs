using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PingTest
{
    class Program
    {
        #region Fields

        private const           string                  _Path             = @"C:\Users\Itamar Katz\Desktop\pings\PingLog.txt";
        private const           string                  _NameOrAddress    = @"213.57.2.5";
        private const           int                     _RequrentTimeMs   = 1000;
        private const           int                     _BasicTimeoutMs   = 500;
        private const           int                     _OnErrorTimeoutMs = 200;
        private static readonly TimeSpan                _ToFileDelay      = TimeSpan.FromSeconds(30);
        private static          Stopwatch               _stopwatch;
        private static          TimeSpan                _totalDownTimeElapsed = TimeSpan.Zero;
        private static          CancellationTokenSource _tokenSource          = new CancellationTokenSource();
        private static          string                  GetFormattedTime => $"{DateTime.Now:dd/MM/yyyy - hh:mm:ss} -";

        #endregion Fields

        // private static void Main()
        // {
        //     // System.Globalization.CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        //     foreach ( double d in Enumerable.Range(0, 100).Select(x=> (double)x/100) ) {
        //         Console.WriteLine($"{d}\t| \t{d:P4}");
        //         Task.Delay(5).Wait();
        //     }
        //     // for ( double i = 0; i < 1000; i = i+10 ) {
        //     //     double j = 1 / ( 1000 - i );
        //     //     Console.WriteLine($"i:{i}\t|\tj:{j}\t|\t{j*10:P3}%");
        //     //     Task.Delay(5).Wait();
        //     // }
        // }
        private static void Main()
        {
            if ( !File.Exists(_Path) ) {
                Console.WriteLine($"Error: File does not exist. File is: {_Path}");

                return;
            }

            _tokenSource = new CancellationTokenSource();
            _stopwatch   = Stopwatch.StartNew();
            PrintUsage();

            Task t1 = Task.Factory.StartNew(async () =>
                                            {
                                                while ( true ) {
                                                    await PingHostTest(_tokenSource.Token);
                                                    await Task.Delay(_RequrentTimeMs, _tokenSource.Token);
                                                }
                                            },
                                            _tokenSource.Token);

            Task t2 = Task.Factory.StartNew(() =>
                                            {
                                                try {
                                                    while ( true ) {
                                                        ConsoleKeyInfo input = Console.ReadKey();
                                                        Console.WriteLine();

                                                        switch ( input.Key ) {
                                                            case ConsoleKey.S :
                                                                Stop();

                                                                break;
                                                            case ConsoleKey.B :
                                                                Begin();

                                                                break;
                                                            case ConsoleKey.R :
                                                                TotalReportToConsole();

                                                                break;
                                                            case ConsoleKey.C :
                                                                Clear();

                                                                break;
                                                            case ConsoleKey.H :
                                                                PrintUsage();

                                                                break;
                                                            case ConsoleKey.E :
                                                                Exit();

                                                                break;
                                                            case ConsoleKey.W :
                                                                WriteToFile();

                                                                break;
                                                            default :
                                                                PrintUsage();

                                                                break;
                                                        }
                                                    }
                                                }
                                                catch ( Exception exception ) {
                                                    Console.WriteLine(exception.Message);
                                                }
                                            },
                                            _tokenSource.Token);

            Task t3 = Task.Factory.StartNew(async () =>
                                            {
                                                while ( true ) {
                                                    if ( !_tokenSource.IsCancellationRequested ) {
                                                        WriteToFile();
                                                    }

                                                    await Task.Delay(_ToFileDelay);
                                                }
                                            },
                                            _tokenSource.Token);

            Task.WaitAll(t1, t2, t3);
        }

        #region Control / Report

        private static void Begin()
        {
            _stopwatch.Start();
            _tokenSource = new CancellationTokenSource();
        }

        private static void Stop()
        {
            _tokenSource.Cancel();
            _stopwatch.Stop();
        }

        private static void Clear()
        {
            TotalReportToConsole();
            _stopwatch.Restart();
            _totalDownTimeElapsed = TimeSpan.Zero;
        }

        private static void Exit()
        {
            Stop();
            Console.WriteLine("You are about to exit. Are you sure? (y to confirm)");
            ConsoleKeyInfo input = Console.ReadKey();
            Console.WriteLine();

            if ( input.Key == ConsoleKey.Y ) {
                WriteToFile();
                Environment.Exit(0);
            }

            Begin();
        }

        private static string GetTotalReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{GetFormattedTime} Total down time is:\t\t{Format(_totalDownTimeElapsed)}");
            double downtime = ( double ) _totalDownTimeElapsed.Ticks / _stopwatch.Elapsed.Ticks;
            sb.AppendLine($"{GetFormattedTime} Percentage of downtime is:\t{downtime:P4}");
            sb.AppendLine($"{GetFormattedTime} Total running time is:\t\t{Format(_stopwatch.Elapsed)}");

            return sb.ToString();
        }

        private static void TotalReportToConsole() => Console.WriteLine(GetTotalReport());

        private static void PrintUsage()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Usage:");
            sb.AppendLine("\tS: Stop");
            sb.AppendLine("\tB: Begin");
            sb.AppendLine("\tR: Print Report");
            sb.AppendLine("\tC: Clear timers and restart");
            sb.AppendLine("\tH: Prints this usage");
            sb.AppendLine("\tE: End program");
            sb.AppendLine("\tW: Write to file");
            Console.WriteLine(sb);
        }

        private static readonly object fileLocker = false;

        private static void WriteToFile()
        {
            lock ( fileLocker ) {
                try {
                    using StreamWriter streamWriter = File.AppendText(_Path);
                    streamWriter.WriteLine(GetTotalReport());
                    streamWriter.Flush();
                    // await using StreamWriter streamWriter = File.AppendText(_Path);
                    // await streamWriter.WriteLineAsync(GetTotalReport());
                    // await streamWriter.FlushAsync();
                }
                catch ( Exception exception ) {
                    Console.WriteLine(exception.Message);
                }
            }
        }

        #endregion Control / Report

        #region Actual Logic

        public static async Task PingHostTest( CancellationToken cancellationToken )
        {
            if ( cancellationToken.IsCancellationRequested ) { return; }

            try {
                using Ping ping     = new Ping();
                TimeSpan   failTime = _stopwatch.Elapsed;
                PingReply  reply    = await ping.SendPingAsync(_NameOrAddress, _BasicTimeoutMs, new byte[32], new PingOptions(64, true));

                if ( !IsSuccess(reply) ) {
                    Console.WriteLine("\n------------- Connection Problem Detected -------------\n");
                    TimeSpan downTimeElapsed = new TimeSpan(_stopwatch.Elapsed.Ticks - failTime.Ticks);
                    Console.WriteLine($"{GetFormattedTime} Current down time is:\t\t{Format(downTimeElapsed)}");

                    while ( !cancellationToken.IsCancellationRequested &&
                            !IsSuccess(await ping.SendPingAsync(_NameOrAddress, _OnErrorTimeoutMs, new byte[32], new PingOptions(64, true))) ) {
                        downTimeElapsed = new TimeSpan(_stopwatch.Elapsed.Ticks - failTime.Ticks);
                        Console.WriteLine($"{GetFormattedTime} Current down time is:\t\t{Format(downTimeElapsed)}");
                        await Task.Delay(_OnErrorTimeoutMs, cancellationToken);
                    }

                    Console.WriteLine("\nReport:\n");
                    downTimeElapsed       = new TimeSpan(_stopwatch.Elapsed.Ticks - failTime.Ticks);
                    _totalDownTimeElapsed = new TimeSpan(downTimeElapsed.Ticks + _totalDownTimeElapsed.Ticks);
                    Console.WriteLine($"{GetFormattedTime} Current down time was:\t\t{Format(downTimeElapsed)}");
                    TotalReportToConsole();
                    Console.WriteLine("-------------- End of Connection Problem --------------\n");
                }

                string message =
                    $"{GetFormattedTime} Reply from {reply?.Address}: bytes={reply?.Buffer?.Length} time={reply?.RoundtripTime}ms TTL={reply?.Options?.Ttl}";

                Console.WriteLine(message);
            }
            catch ( PingException e ) {
                string message = $"Ping Exception Occurred:\n{e}!";
                Console.WriteLine(message);
                Console.Write("Press enter to continue..");
                Console.ReadLine();
            }
        }

        private static bool IsSuccess( PingReply reply )
        {
            if ( reply == null ) {
                Console.WriteLine($"{GetFormattedTime} Ping is Null...");
            }
            else if ( reply.Status != IPStatus.Success ) {
                Console.WriteLine($"{GetFormattedTime} Ping Not Successful. Ping Reply Enum:\t{reply.Status}");
            }
            else { return true; }

            return false;
        }

        #endregion Actual Logic

        private static string Format( TimeSpan timeSpan ) => $"{timeSpan.Hours}:{timeSpan.Minutes}:{timeSpan.Seconds}.{timeSpan.Milliseconds} [H:M:S.ms]";
    }
}