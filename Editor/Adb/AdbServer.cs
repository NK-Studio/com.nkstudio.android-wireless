using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AndroidWireless
{
    /// <summary>adb 서버가 요청을 FAIL로 거절함(지원하지 않는 서비스 등).</summary>
    public sealed class AdbFailException : Exception
    {
        public AdbFailException(string message) : base(message) { }
    }

    /// <summary>
    /// adb 서버(기본 127.0.0.1:5037)와의 소켓 연결 하나. 요청은 "{길이 4자리 hex}{서비스}",
    /// 응답은 "OKAY" 또는 "FAIL{길이}{메시지}", 이후 데이터는 "{길이 4자리 hex}{payload}" 프레임.
    /// </summary>
    internal sealed class AdbConnection : IDisposable
    {
        private readonly TcpClient client;
        private readonly NetworkStream stream;
        private readonly CancellationToken token;
        private CancellationTokenRegistration registration;

        private AdbConnection(TcpClient client, CancellationToken token)
        {
            this.client = client;
            this.token = token;
            stream = client.GetStream();
            // NetworkStream은 취소 토큰을 무시하므로 소켓을 닫아 대기 중인 읽기를 깨운다.
            registration = token.Register(client.Dispose);
        }

        public static async Task<AdbConnection> OpenAsync(string service, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var client = new TcpClient { NoDelay = true };
            AdbConnection connection = null;
            try
            {
                using (token.Register(client.Dispose))
                    await client.ConnectAsync(IPAddress.Loopback, AdbServer.Port);

                connection = new AdbConnection(client, token);
                byte[] request = Encoding.ASCII.GetBytes(service.Length.ToString("x4") + service);
                await connection.stream.WriteAsync(request, 0, request.Length);

                string status = Encoding.ASCII.GetString(await connection.ReadExactAsync(4));
                if (status == "OKAY") return connection;

                string message = status == "FAIL" ? await connection.ReadStringAsync() : "알 수 없는 응답: " + status;
                connection.Dispose();
                throw new AdbFailException(message ?? "FAIL");
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                connection?.Dispose();
                client.Dispose();
                throw new OperationCanceledException(token);
            }
            catch (Exception e) when (!(e is AdbFailException))
            {
                connection?.Dispose();
                client.Dispose();
                throw;
            }
        }

        /// <summary>길이 프레임 하나를 읽는다. 서버가 연결을 닫으면 null.</summary>
        public async Task<byte[]> ReadFrameAsync()
        {
            byte[] header = await ReadExactAsync(4, allowEof: true);
            if (header == null) return null;
            int length = Convert.ToInt32(Encoding.ASCII.GetString(header), 16);
            return length == 0 ? Array.Empty<byte>() : await ReadExactAsync(length);
        }

        /// <summary>
        /// 단발 요청의 응답 문자열. host:pair처럼 OKAY 뒤에 다시 "OKAY"/"FAIL" 상태를 붙여 보내는 서비스도 처리한다.
        /// </summary>
        public async Task<string> ReadReplyAsync()
        {
            byte[] header = await ReadExactAsync(4, allowEof: true);
            if (header == null) return "";
            string head = Encoding.ASCII.GetString(header);
            if (head == "FAIL") throw new AdbFailException(await ReadStringAsync() ?? "FAIL");
            if (head == "OKAY") return await ReadStringAsync() ?? "";

            int length = Convert.ToInt32(head, 16);
            return length == 0 ? "" : Encoding.UTF8.GetString(await ReadExactAsync(length));
        }

        public async Task<string> ReadStringAsync()
        {
            byte[] frame = await ReadFrameAsync();
            return frame == null ? null : Encoding.UTF8.GetString(frame);
        }

        private async Task<byte[]> ReadExactAsync(int count, bool allowEof = false)
        {
            var buffer = new byte[count];
            int read = 0;
            try
            {
                while (read < count)
                {
                    int n = await stream.ReadAsync(buffer, read, count - read);
                    if (n == 0)
                    {
                        if (allowEof && read == 0) return null;
                        throw new EndOfStreamException("adb 서버가 연결을 닫았습니다.");
                    }
                    read += n;
                }
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }
            return buffer;
        }

        public void Dispose()
        {
            registration.Dispose();
            client.Dispose();
        }
    }

    /// <summary>adb CLI를 거치지 않고 실행 중인 adb 서버에 직접 요청한다. (CLI 버전 차이로 서버가 재시작되는 문제를 피함)</summary>
    public static class AdbServer
    {
        private static readonly Regex PairedGuidRegex = new Regex(@"\[guid=([^\]]+)\]");

        public static int Port
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("ANDROID_ADB_SERVER_PORT");
                return int.TryParse(env, out int port) && port > 0 && port < 65536 ? port : 5037;
            }
        }

        /// <summary>요청 하나를 보내고 응답 문자열 하나를 받는다 (adb_query와 동일).</summary>
        public static async Task<string> QueryAsync(string service, CancellationToken token)
        {
            using (var connection = await AdbConnection.OpenAsync(service, token))
                return await connection.ReadReplyAsync();
        }

        /// <summary>서버가 떠 있지 않으면 `adb start-server`로 띄운다. 실패 시 이유를 돌려준다.</summary>
        public static async Task<string> EnsureRunningAsync(CancellationToken token)
        {
            try
            {
                await QueryAsync("host:version", token);
                return null;
            }
            catch (SocketException)
            {
                // 서버 없음 → 아래에서 시작
            }

            if (string.IsNullOrEmpty(AdbClient.ResolveAdbPath()))
                return Localization.Get("error.noAdb");

            var result = await AdbClient.RunAsync("start-server", token, 20000);
            if (result.Success) return null;

            // 종료 코드나 출력이 애매해도 서버가 실제로 떠 있으면 성공으로 본다.
            try
            {
                await QueryAsync("host:version", token);
                return null;
            }
            catch (SocketException)
            {
                return Localization.Format("error.startServer", result.Output);
            }
        }

        /// <summary>
        /// 실행 중인 adb 서버를 내리고 포트가 닫힐 때까지 기다린다. Windows에서는 실행 중인 adb.exe를
        /// 덮어쓸 수 없으므로 platform-tools를 설치하기 전에 호출한다.
        /// </summary>
        public static async Task KillAsync(CancellationToken token, int waitMs = 5000)
        {
            try
            {
                await QueryAsync("host:kill", token);
            }
            catch (SocketException)
            {
                return; // 서버가 떠 있지 않음
            }
            catch (Exception e) when (e is IOException || e is AdbFailException)
            {
                // 서버가 응답 없이 연결을 끊으며 종료한 경우
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(waitMs);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    await QueryAsync("host:version", token);
                }
                catch (SocketException)
                {
                    break; // 포트가 닫힘
                }
                catch (IOException) { }
                await Task.Delay(200, token);
            }

            // 소켓이 닫힌 뒤에도 프로세스가 exe 파일을 잠깐 잡고 있을 수 있다.
            await Task.Delay(500, token);
        }

        /// <summary>성공 시 연결 서비스 이름(guid)을 돌려준다.</summary>
        public static async Task<(bool ok, string guid, string message)> PairAsync(string address, string code, CancellationToken token)
        {
            string output;
            try
            {
                output = await QueryAsync($"host:pair:{code}:{address}", token);
            }
            catch (AdbFailException e)
            {
                return (false, null, e.Message);
            }

            bool ok = output.IndexOf("Successfully paired", StringComparison.OrdinalIgnoreCase) >= 0;
            var guid = PairedGuidRegex.Match(output);
            return (ok, guid.Success ? guid.Groups[1].Value : null, output);
        }

        public static async Task<(bool ok, string message)> ConnectAsync(string address, CancellationToken token)
        {
            string output;
            try
            {
                output = await QueryAsync("host:connect:" + address, token);
            }
            catch (AdbFailException e)
            {
                return (false, e.Message);
            }

            bool ok = output.IndexOf("connected to", StringComparison.OrdinalIgnoreCase) >= 0
                      && output.IndexOf("failed", StringComparison.OrdinalIgnoreCase) < 0
                      && output.IndexOf("cannot", StringComparison.OrdinalIgnoreCase) < 0;
            return (ok, output);
        }
    }
}
