// 
// Copyright (c) 2010-2012 Logentries, Jlizard
// 
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without 
// modification, are permitted provided that the following conditions 
// are met:
// 
// * Redistributions of source code must retain the above copyright notice, 
//   this list of conditions and the following disclaimer. 
// 
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution. 
// 
// * Neither the name of Logentries nor the names of its 
//   contributors may be used to endorse or promote products derived from this
//   software without specific prior written permission. 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
// ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF 
// THE POSSIBILITY OF SUCH DAMAGE.
// 
// Mark Lacomber <marklacomber@gmail.com>
// Viliam Holub <vilda@logentries.com>

/*
 *   VERSION:  2.3.5
 */


﻿using System;
using System.Collections.Concurrent;
using System.Configuration;
﻿using System.Linq;
﻿using System.Text.RegularExpressions;
using System.Text;
﻿using System.Net.Security;
using System.Net.Sockets;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using log4net.Core;
using log4net.Util;

namespace log4net.Appender
{
    public class LogentriesAppender : AppenderSkeleton
    {
        /*
         * Constants
         */

        /** Current version number  */
        public const String VERSION = "2.3.5";
        /** Size of the internal event queue. */
        const int QUEUE_SIZE = 32768;
        /** Logentries API server address. */
        const String LE_API = "api.logentries.com";
        /** Port number for token logging on Logentries API server. */
        const int LE_TOKEN_PORT = 10000;
        /** Port number for TLS encrypted token logging on Logentries API server */
        const int LE_TOKEN_TLS_PORT = 20000;
        /** Port number for http PUT logging on Logentries API server. */
        const int LE_HTTP_PORT = 80;
        /** Port number for SSL HTTP PUT logging on Logentries API server. */
        const int LE_HTTP_SSL_PORT = 443;
        /** UTF-8 output character set. */
        static readonly UTF8Encoding UTF8 = new UTF8Encoding();
        /** ASCII character set used by HTTP. */
        static readonly ASCIIEncoding ASCII = new ASCIIEncoding();
        /** Minimal delay between attempts to reconnect in milliseconds. */
        const int MIN_DELAY = 100;
        /** Maximal delay between attempts to reconnect in milliseconds. */
        const int MAX_DELAY = 10000;
        /** LE appender signature - used for debugging messages. */
        const String LE = "LE: ";
        /** Logentries Config Token */
        const String CONFIG_TOKEN = "LOGENTRIES_TOKEN";
        /** Logentries Config Account Key */
        const String CONFIG_ACCOUNT_KEY = "LOGENTRIES_ACCOUNT_KEY";
        /** Logentries Config Location */
        const String CONFIG_LOCATION = "LOGENTRIES_LOCATION";
        /** Error message displayed when invalid token is detected. */
        const String INVALID_TOKEN = "\n\nIt appears your LOGENTRIES_TOKEN parameter in web/app.config is invalid!\n\n";
        /** Error message displayed when invalid account_key or location parameters are detected */
        const String INVALID_HTTP_PUT = "\n\nIt appears your LOGENTRIES_ACCOUNT_KEY or LOGENTRIES_LOCATION parameters in web/app.config are invalid!\n\n";
        /** Error message deisplayed when queue overflow occurs */
        const String QUEUE_OVERFLOW = "\n\nLogentries Buffer Queue Overflow. Message Dropped!\n\n";

        /** Logentries API Server Certificate */
        static readonly X509Certificate2 LE_API_CERT = new X509Certificate2(Encoding.UTF8.GetBytes(@"-----BEGIN CERTIFICATE-----
MIIFSjCCBDKgAwIBAgIDBQMSMA0GCSqGSIb3DQEBBQUAMGExCzAJBgNVBAYTAlVT
MRYwFAYDVQQKEw1HZW9UcnVzdCBJbmMuMR0wGwYDVQQLExREb21haW4gVmFsaWRh
dGVkIFNTTDEbMBkGA1UEAxMSR2VvVHJ1c3QgRFYgU1NMIENBMB4XDTEyMDkxMDE5
NTI1N1oXDTE2MDkxMTIxMjgyOFowgcExKTAnBgNVBAUTIEpxd2ViV3RxdzZNblVM
ek1pSzNiL21hdktiWjd4bEdjMRMwEQYDVQQLEwpHVDAzOTM4NjcwMTEwLwYDVQQL
EyhTZWUgd3d3Lmdlb3RydXN0LmNvbS9yZXNvdXJjZXMvY3BzIChjKTEyMS8wLQYD
VQQLEyZEb21haW4gQ29udHJvbCBWYWxpZGF0ZWQgLSBRdWlja1NTTChSKTEbMBkG
A1UEAxMSYXBpLmxvZ2VudHJpZXMuY29tMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8A
MIIBCgKCAQEAxcmFqgE2p6+N9lM2GJhe8bNUO0qmcw8oHUVrsneeVA66hj+qKPoJ
AhGKxC0K9JFMyIzgPu6FvuVLahFZwv2wkbjXKZLIOAC4o6tuVb4oOOUBrmpvzGtL
kKVN+sip1U7tlInGjtCfTMWNiwC4G9+GvJ7xORgDpaAZJUmK+4pAfG8j6raWgPGl
JXo2hRtOUwmBBkCPqCZQ1mRETDT6tBuSAoLE1UMlxWvMtXCUzeV78H+2YrIDxn/W
xd+eEvGTSXRb/Q2YQBMqv8QpAlarcda3WMWj8pkS38awyBM47GddwVYBn5ZLEu/P
DiRQGSmLQyFuk5GUdApSyFETPL6p9MfV4wIDAQABo4IBqDCCAaQwHwYDVR0jBBgw
FoAUjPTZkwpHvACgSs5LdW6gtrCyfvwwDgYDVR0PAQH/BAQDAgWgMB0GA1UdJQQW
MBQGCCsGAQUFBwMBBggrBgEFBQcDAjAdBgNVHREEFjAUghJhcGkubG9nZW50cmll
cy5jb20wQQYDVR0fBDowODA2oDSgMoYwaHR0cDovL2d0c3NsZHYtY3JsLmdlb3Ry
dXN0LmNvbS9jcmxzL2d0c3NsZHYuY3JsMB0GA1UdDgQWBBRaMeKDGSFaz8Kvj+To
j7eMOtT/zTAMBgNVHRMBAf8EAjAAMHUGCCsGAQUFBwEBBGkwZzAsBggrBgEFBQcw
AYYgaHR0cDovL2d0c3NsZHYtb2NzcC5nZW90cnVzdC5jb20wNwYIKwYBBQUHMAKG
K2h0dHA6Ly9ndHNzbGR2LWFpYS5nZW90cnVzdC5jb20vZ3Rzc2xkdi5jcnQwTAYD
VR0gBEUwQzBBBgpghkgBhvhFAQc2MDMwMQYIKwYBBQUHAgEWJWh0dHA6Ly93d3cu
Z2VvdHJ1c3QuY29tL3Jlc291cmNlcy9jcHMwDQYJKoZIhvcNAQEFBQADggEBAAo0
rOkIeIDrhDYN8o95+6Y0QhVCbcP2GcoeTWu+ejC6I9gVzPFcwdY6Dj+T8q9I1WeS
VeVMNtwJt26XXGAk1UY9QOklTH3koA99oNY3ARcpqG/QwYcwaLbFrB1/JkCGcK1+
Ag3GE3dIzAGfRXq8fC9SrKia+PCdDgNIAFqe+kpa685voTTJ9xXvNh7oDoVM2aip
v1xy+6OfZyGudXhXag82LOfiUgU7hp+RfyUG2KXhIRzhMtDOHpyBjGnVLB0bGYcC
566Nbe7Alh38TT7upl/O5lA29EoSkngtUWhUnzyqYmEMpay8yZIV4R9AuUk2Y4HB
kAuBvDPPm+C0/M4RLYs=
-----END CERTIFICATE-----"));

        readonly Random random = new Random();

        /** Custom socket class to allow for choice of Token-based logging and HTTP PUT */
        private LogentriesTcpClient tcp_client = null;
        /** Thread used for background polling of log queue */
        public Thread thread;
        /** Asynchronous logging started flag */
        public bool started = false;
        /** Logentries Token Parameter */
        private String m_Token = "";
        /** Logentries Account Key Parameter */
        private String m_Key = "";
        /** Logentries Location Parameter */
        private String m_Location = "";
        /** Logentries HTTP PUT flag parameter */
        private bool m_HttpPut = false;
        /** Logentries SSL/TLS flag parameter */
        private bool m_Ssl = false;
        /** Message Queue. */
        public BlockingCollection<string> queue;
        /** Newline char to trim from message for formatting */
        static char[] trimChars = { '\n' };
        /** Logentries Debug flag parameter */
        private bool m_Debug;

        #region Public Instance Properties

        /** Debug flag. */
        public bool Debug
        {
            get { return m_Debug; }
            set { m_Debug = value; }
        }

        /** Option to set Token programmatically or in Appender Definition */
        public string Token
        {
            get { return m_Token; }
            set { m_Token = value; }
        }

        /** HTTP PUT Flag */
        public bool HttpPut
        {
            get { return m_HttpPut; }
            set { m_HttpPut = value; }
        }

        /** ACCOUNT_KEY parameter for HTTP PUT logging */
        public String Key
        {
            get { return m_Key; }
            set { m_Key = value; }
        }

        /** LOCATION parameter for HTTP PUT logging */
        public String Location
        {
            get { return m_Location; }
            set { m_Location = value; }
        }

        /** SSL/TLS parameter */
        public bool Ssl
        {
            get { return m_Ssl; }
            set { m_Ssl = value; }
        }

        #endregion

        #region Constructor

        public LogentriesAppender()
        {
            queue = new BlockingCollection<string>(QUEUE_SIZE);

            thread = new Thread(new ThreadStart(run_loop));
            thread.Name = "Logentries Log4net Appender";
            thread.IsBackground = true;
        }

        #endregion

        private void openConnection()
        {
            try
            {
                if (this.tcp_client == null)
                    this.tcp_client = new LogentriesTcpClient(HttpPut, Ssl);

                this.tcp_client.Connect();

                if (HttpPut)
                {
                    String header = String.Format("PUT /{0}/hosts/{1}/?realtime=1 HTTP/1.1\r\n\r\n", this.m_Key, this.m_Location);
                    this.tcp_client.Write(ASCII.GetBytes(header), 0, header.Length);
                }
            }
            catch
            {
                throw new IOException();
            }
        }

        private void reopenConnection()
        {
            closeConnection();

            int root_delay = MIN_DELAY;
            while (true)
            {
                try
                {
                    openConnection();

                    return;
                }
                catch (Exception e)
                {
                    if (Debug)
                    {
                        WriteDebugMessages("Unable to connect to Logentries");
                    }
                }

                root_delay *= 2;
                if (root_delay > MAX_DELAY)
                    root_delay = MAX_DELAY;
                int wait_for = root_delay + random.Next(root_delay);

                try
                {
                    Thread.Sleep(wait_for);
                }
                catch
                {
                    throw new ThreadInterruptedException();
                }
            }
        }

        private void closeConnection()
        {
            if (this.tcp_client != null)
                this.tcp_client.Close();
        }

        public void run_loop()
        {
            try
            {
                //Open connection
                reopenConnection();

                //Send data in queue
                while (true)
                {
                    //Take data from queue
                    string line = queue.Take();
                    //Replace newline chars with line separator to format multi-line events nicely
                    line = line.Replace(System.Environment.NewLine, "\u2028");

                    string final_line = (!HttpPut ? this.Token + line : line) + '\n';

                    byte[] data = LogentriesAppender.UTF8.GetBytes(final_line);

                    //Send data, reconnect if needed
                    while (true)
                    {
                        try
                        {
                            this.tcp_client.Write(data, 0, data.Length);
                        }
                        catch (IOException e)
                        {
                            //Reopen the lost connection
                            reopenConnection();
                            continue;
                        }
                        break;
                    }
                }
            }
            catch (ThreadInterruptedException e)
            {
                WriteDebugMessages("Logentries asynchronous socket client interrupted");
            }
        }

        public void addLine(String line)
        {
            WriteDebugMessages("Queueing " + line);

            //Try to append data to queue
            if (!queue.TryAdd(line))
            {
                queue.Take();
                if (!queue.TryAdd(line))
                    WriteDebugMessages(QUEUE_OVERFLOW);
            }
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (!started && checkCredentials())
            {
                WriteDebugMessages("Starting Logentries asynchronous socket client");
                thread.Start();
                started = true;
            }

            //Render message content
            String renderedEvent = RenderLoggingEvent(loggingEvent);

            renderedEvent = renderedEvent.TrimEnd(trimChars);

            addLine(renderedEvent);

        }

        protected override void Append(LoggingEvent[] loggingEvents)
        {
            foreach (LoggingEvent logEvent in loggingEvents)
            {
                this.Append(logEvent);
            }
        }

        protected override bool RequiresLayout
        {
            get { return true; }
        }

        protected override void OnClose()
        {
            thread.Interrupt();
        }

        public bool checkCredentials()
        {
            var appSettings = ConfigurationManager.AppSettings;

            if (!HttpPut)
            {
                if (checkValidUUID(this.m_Token))
                    return true;

                if (appSettings.AllKeys.Contains(CONFIG_TOKEN) && checkValidUUID(appSettings[CONFIG_TOKEN]))
                {
                    this.m_Token = appSettings[CONFIG_TOKEN];
                    return true;
                }

                WriteDebugMessages(INVALID_TOKEN);
                return false;
            }

            if (this.m_Key != "" && checkValidUUID(this.m_Key) && this.m_Location != "")
                return true;

            if (appSettings.AllKeys.Contains(CONFIG_ACCOUNT_KEY) && checkValidUUID(appSettings[CONFIG_ACCOUNT_KEY]))
            {
                this.m_Key = appSettings[CONFIG_ACCOUNT_KEY];

                if (appSettings.AllKeys.Contains(CONFIG_LOCATION) && appSettings[CONFIG_LOCATION] != "")
                {
                    this.m_Location = appSettings[CONFIG_LOCATION];
                    return true;
                }
            }

            WriteDebugMessages(INVALID_HTTP_PUT);
            return false;
        }

        public bool checkValidUUID(string uuid_input)
        {
            if (uuid_input == "")
                return false;

            System.Guid newGuid = System.Guid.NewGuid();

            return System.Guid.TryParse(uuid_input, out newGuid);
        }
		
		//Used for UnitTests, Append method is protected
		public void TestAppend(LoggingEvent logEvent)
		{
			this.Append(logEvent);
		}

        private void WriteDebugMessages(string message, Exception e)
        {
            message = LE + message;
            if (!Debug) 
               return;
            string[] messages = {message, e.ToString()};
            foreach (var msg in messages)
            {
                //Use below line instead when compiling with log4net1.2.10
                //LogLog.Debug(msg);
                LogLog.Debug(typeof(LogentriesAppender), msg);
            }
        }

        private void WriteDebugMessages(string message)
        {
            if (!Debug)
                return;

            message = LE + message;

            //Use below line instead when compiling with log4net1.2.10
            //LogLog.Debug(message);
	        LogLog.Debug(typeof(LogentriesAppender), message);
        }

        private string SubstituteAppSetting(string key)
        {
            var appSettings = ConfigurationManager.AppSettings;
            if (appSettings.HasKeys() && appSettings.AllKeys.Contains(key))
            {
                return appSettings[key];
            }
            else
            {
                return key;
            }
        }

        /** Custom Class to support both HTTP PUT and Token-based logging as well as TLS/SSL */
        private class LogentriesTcpClient
        {
            private TcpClient client = null;
            private Stream stream = null;
            private SslStream ssl_stream = null;
            private bool ssl_choice = false;
            private int port;

            public LogentriesTcpClient(bool httpPut, bool ssl)
            {
                ssl_choice = ssl;
                if (!ssl)
                    port = httpPut ? LE_HTTP_PORT : LE_TOKEN_PORT;
                else
                    port = httpPut ? LE_HTTP_SSL_PORT : LE_TOKEN_TLS_PORT;
            }

            private Stream getTheStream()
            {
                return ssl_choice ? ssl_stream : stream;
            }

            public void Connect()
            {
                this.client = new TcpClient(LE_API, port);
                this.client.NoDelay = true;

                this.stream = client.GetStream();

                if (ssl_choice)
                {
                    this.ssl_stream = new SslStream(this.stream, false, (sender, cert, chain, errors) => cert.GetCertHashString() == LE_API_CERT.GetCertHashString());

                    this.ssl_stream.AuthenticateAsClient(LE_API);
                }
            }

            public void Write(byte[] buffer, int offset, int count)
            {
                this.getTheStream().Write(buffer, offset, count);
            }

            public void Flush()
            {
                this.getTheStream().Flush();
            }

            public void Close()
            {
                if (this.client != null)
                {
                    try
                    {
                        this.client.Close();
                    }
                    catch { }
                }
            }
        }
    }
}
