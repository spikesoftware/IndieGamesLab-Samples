using IGL.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace IGL.Client
{
    public abstract class ServiceBusBase
    {
        static object _lock = new object();
        static bool _tokenRequested;
        static string _token;
        static DateTime _tokenExpiresOn;

        public static string Token
        {
            get
            {
                if (_token == null || _tokenExpiresOn < DateTime.UtcNow)
                {
                    // reset token in the case of expiry
                    _token = null;

                    if (!_tokenRequested)
                    {
                        lock (_lock)
                        {
                            _tokenRequested = true;
                            GetToken();
                        }
                    }
                }

                return _token;
            }
        }

        private static void GetToken()
        {
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = RemoteCertificateValidationCallback;

                var acsEndpoint = CommonConfiguration.Instance.BackboneConfiguration.GetACSEndpoint();

                // Note that the realm used when requesting a token uses the HTTP scheme, even though
                // calls to the service are always issued over HTTPS
                var realm = CommonConfiguration.Instance.BackboneConfiguration.GetRealm();

                NameValueCollection values = new NameValueCollection();
                values.Add("wrap_name", CommonConfiguration.Instance.BackboneConfiguration.IssuerName);
                values.Add("wrap_password", CommonConfiguration.Instance.BackboneConfiguration.IssuerSecret);
                values.Add("wrap_scope", realm);

                using (WebClient webClient = new WebClient())
                {
                    webClient.UploadValuesCompleted += WebClient_UploadValuesCompleted; ;
                    webClient.UploadValuesAsync(new Uri(acsEndpoint), values);
                }
            }
            catch(Exception)
            {
                // TODO: log exception
                _tokenRequested = false;
            }
        }

        private static void WebClient_UploadValuesCompleted(object sender, UploadValuesCompletedEventArgs e)
        {
            try
            {
                if (e.Error == null)
                {
                    string responseString = Encoding.UTF8.GetString(e.Result);

                    var responseProperties = responseString.Split('&');
                    var tokenProperty = responseProperties[0].Split('=');
                    var token = Uri.UnescapeDataString(tokenProperty[1]);

                    var properties = (from prop in token.Split('&')
                                      let pair = prop.Split(new[] { '=' }, 2)
                                      select new { Name = pair[0], Value = pair[1] })
                                      .ToDictionary(p => p.Name, p => p.Value);

                    var epochStart = new DateTime(1970, 01, 01, 0, 0, 0, 0, DateTimeKind.Utc);

                    _tokenExpiresOn = epochStart.AddSeconds(int.Parse(properties["ExpiresOn"]));
                    _token = "WRAP access_token=\"" + token + "\"";
                }
                else
                {
                    // TODO: log exception
                }
            }
            catch(Exception)
            {
                // TODO: log exception                            
            }
            _tokenRequested = false;
        }

        private static bool RemoteCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            //Return true if the server certificate is ok
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            bool acceptCertificate = true;

            //The server did not present a certificate
            if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNotAvailable) == SslPolicyErrors.RemoteCertificateNotAvailable)
            {
                acceptCertificate = false;
            }
            else
            {
                //The certificate does not match the server name
                if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) == SslPolicyErrors.RemoteCertificateNameMismatch)
                {
                    acceptCertificate = false;
                }

                //There is some other problem with the certificate
                if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.RemoteCertificateChainErrors)
                {
                    foreach (X509ChainStatus item in chain.ChainStatus)
                    {
                        if (item.Status != X509ChainStatusFlags.RevocationStatusUnknown && item.Status != X509ChainStatusFlags.OfflineRevocation)
                            break;

                        if (item.Status != X509ChainStatusFlags.NoError)
                        {
                            acceptCertificate = false;
                        }
                    }
                }
            }
            
            if (acceptCertificate == false)
            {                
                acceptCertificate = true;
            }

            return acceptCertificate;
        }
    }
}
