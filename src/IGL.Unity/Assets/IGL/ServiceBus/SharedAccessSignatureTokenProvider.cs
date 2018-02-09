using System.Net.Security;
using Microsoft.ServiceBus;
using UnityEngine;

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Net;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;

    /// <summary>
    /// The SharedAccessSignatureTokenProvider generates tokens using a shared access key or existing signature.
    /// </summary>
    public class SharedAccessSignatureTokenProvider : TokenProvider
    {
        /// <summary>
        /// Represents 00:00:00 UTC Thursday 1, January 1970.
        /// </summary>
        public static readonly DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        readonly byte[] encodedSharedAccessKey;
        readonly string keyName;
        readonly TimeSpan tokenTimeToLive;
        readonly string sharedAccessSignature;

        internal SharedAccessSignatureTokenProvider(string sharedAccessSignature) 
            : base(TokenScope.Entity)
        {
            SharedAccessSignatureToken.Validate(sharedAccessSignature);
            this.sharedAccessSignature = sharedAccessSignature;
        }

        internal SharedAccessSignatureTokenProvider(string keyName, string sharedAccessKey, TimeSpan tokenTimeToLive)
            : this(keyName, sharedAccessKey, tokenTimeToLive, TokenScope.Entity)
        {
        }

        internal SharedAccessSignatureTokenProvider(string keyName, string sharedAccessKey, TimeSpan tokenTimeToLive, TokenScope tokenScope)
            : this(keyName, sharedAccessKey, TokenProvider.MessagingTokenProviderKeyEncoder, tokenTimeToLive, tokenScope)
        {
        }

        /// <summary></summary>
        /// <param name="keyName"></param>
        /// <param name="sharedAccessKey"></param>
        /// <param name="customKeyEncoder"></param>
        /// <param name="tokenTimeToLive"></param>
        /// <param name="tokenScope"></param>
        protected SharedAccessSignatureTokenProvider(string keyName, string sharedAccessKey, Func<string, byte[]> customKeyEncoder, TimeSpan tokenTimeToLive, TokenScope tokenScope)
            : base(tokenScope)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                throw new ArgumentNullException("keyName");
            }

            if (keyName.Length > SharedAccessSignatureToken.MaxKeyNameLength)
            {
                throw new ArgumentOutOfRangeException(
                    "keyName",
                    Resources.ArgumentStringTooBig.FormatForUser("keyName", SharedAccessSignatureToken.MaxKeyNameLength));
            }

            if (string.IsNullOrEmpty(sharedAccessKey))
            {
                throw new ArgumentNullException("sharedAccessKey");
            }

            if (sharedAccessKey.Length > SharedAccessSignatureToken.MaxKeyLength)
            {
                throw new ArgumentOutOfRangeException(
                    "sharedAccessKey",
                    Resources.ArgumentStringTooBig.FormatForUser("sharedAccessKey", SharedAccessSignatureToken.MaxKeyLength));
            }

            this.keyName = keyName;
            this.tokenTimeToLive = tokenTimeToLive;
            this.encodedSharedAccessKey = customKeyEncoder != null ?
                customKeyEncoder(sharedAccessKey) :
                TokenProvider.MessagingTokenProviderKeyEncoder(sharedAccessKey);

            ServicePointManager.ServerCertificateValidationCallback = RemoteCertificateValidationCallback;
        }

        /// <summary></summary>
        /// <param name="appliesTo"></param>
        /// <param name="action"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        protected override SecurityToken OnGetToken(string appliesTo, string action, TimeSpan timeout)
        {
            var tokenString = this.BuildSignature(appliesTo);            
            return new SharedAccessSignatureToken(tokenString);
        }

        /// <summary></summary>
        /// <param name="targetUri"></param>
        /// <returns></returns>
        protected virtual string BuildSignature(string targetUri)
        {
            return string.IsNullOrEmpty(this.sharedAccessSignature)
                ? SharedAccessSignatureBuilder.BuildSignature(
                    this.keyName,
                    this.encodedSharedAccessKey,
                    targetUri,
                    this.tokenTimeToLive)
                : this.sharedAccessSignature;
        }

        static class SharedAccessSignatureBuilder
        {
            [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "Uris are normalized to lowercase")]
            public static string BuildSignature(
                string keyName,
                byte[] encodedSharedAccessKey,
                string targetUri,
                TimeSpan timeToLive)
            {
                // Note that target URI is not normalized because in IoT scenario it
                // is case sensitive.
                string expiresOn = BuildExpiresOn(timeToLive);
                string audienceUri = WWW.EscapeURL(targetUri);
                var fields = new string[] { audienceUri, expiresOn };

                // Example string to be signed:
                // http://mynamespace.servicebus.windows.net/a/b/c?myvalue1=a
                // <Value for ExpiresOn>
                string signature = Sign(string.Join("\n", fields), encodedSharedAccessKey);

                // Example returned string:
                // SharedAccessKeySignature
                // sr=ENCODED(http://mynamespace.servicebus.windows.net/a/b/c?myvalue1=a)&sig=<Signature>&se=<ExpiresOnValue>&skn=<KeyName>
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1}={2}&{3}={4}&{5}={6}&{7}={8}",
                    SharedAccessSignatureToken.SharedAccessSignature,
                    SharedAccessSignatureToken.SignedResource,
                    audienceUri,
                    SharedAccessSignatureToken.Signature,
                    WWW.EscapeURL(signature),
                    SharedAccessSignatureToken.SignedExpiry,
                    WWW.EscapeURL(expiresOn),
                    SharedAccessSignatureToken.SignedKeyName,
                    WWW.EscapeURL(keyName));
            }

            static string BuildExpiresOn(TimeSpan timeToLive)
            {
                DateTime expiresOn = DateTime.UtcNow.Add(timeToLive);
                TimeSpan secondsFromBaseTime = expiresOn.Subtract(EpochTime);
                long seconds = Convert.ToInt64(secondsFromBaseTime.TotalSeconds, CultureInfo.InvariantCulture);
                return Convert.ToString(seconds, CultureInfo.InvariantCulture);
            }

            static string Sign(string requestString, byte[] encodedSharedAccessKey)
            {
                using (HMACSHA256 hmac = new HMACSHA256(encodedSharedAccessKey))
                {
                    return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(requestString)));
                }
            }
        }

        /// <summary>
        /// A WCF SecurityToken that wraps a Shared Access Signature
        /// </summary>
        class SharedAccessSignatureToken : SecurityToken
        {
            public const int MaxKeyNameLength = 256;
            public const int MaxKeyLength = 256;
            public const string SharedAccessSignature = "SharedAccessSignature";
            public const string SignedResource = "sr";
            public const string Signature = "sig";
            public const string SignedKeyName = "skn";
            public const string SignedExpiry = "se";
            public const string SignedResourceFullFieldName = SharedAccessSignature + " " + SignedResource;
            public const string SasKeyValueSeparator = "=";
            public const string SasPairSeparator = "&";

            public SharedAccessSignatureToken(string tokenString)
                : base(tokenString)
            {
            }

            protected override string AudienceFieldName
            {
                get
                {
                    return SignedResourceFullFieldName;
                }
            }

            protected override string ExpiresOnFieldName
            {
                get
                {
                    return SignedExpiry;
                }
            }

            protected override string KeyValueSeparator
            {
                get
                {
                    return SasKeyValueSeparator;
                }
            }

            protected override string PairSeparator
            {
                get
                {
                    return SasPairSeparator;
                }
            }

            internal static void Validate(string sharedAccessSignature)
            {
                if (string.IsNullOrEmpty(sharedAccessSignature))
                {
                    throw new ArgumentNullException("sharedAccessSignature");
                }

                IDictionary<string, string> parsedFields = ExtractFieldValues(sharedAccessSignature);

                string signature;
                if (!parsedFields.TryGetValue(Signature, out signature))
                {
                    throw new ArgumentNullException(Signature);
                }

                string expiry;
                if (!parsedFields.TryGetValue(SignedExpiry, out expiry))
                {
                    throw new ArgumentNullException(SignedExpiry);
                }

                string keyName;
                if (!parsedFields.TryGetValue(SignedKeyName, out keyName))
                {
                    throw new ArgumentNullException(SignedKeyName);
                }

                string encodedAudience;
                if (!parsedFields.TryGetValue(SignedResource, out encodedAudience))
                {
                    throw new ArgumentNullException(SignedResource);
                }
            }

            static IDictionary<string, string> ExtractFieldValues(string sharedAccessSignature)
            {
                string[] tokenLines = sharedAccessSignature.Split();

                if (!string.Equals(tokenLines[0].Trim(), SharedAccessSignature, StringComparison.OrdinalIgnoreCase) || tokenLines.Length != 2)
                {
                    throw new ArgumentNullException("sharedAccessSignature");
                }

                IDictionary<string, string> parsedFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] tokenFields = tokenLines[1].Trim().Split(new[] { SasPairSeparator }, StringSplitOptions.None);

                foreach (string tokenField in tokenFields)
                {
                    if (tokenField != string.Empty)
                    {
                        string[] fieldParts = tokenField.Split(new[] { SasKeyValueSeparator }, StringSplitOptions.None);
                        if (string.Equals(fieldParts[0], SignedResource, StringComparison.OrdinalIgnoreCase))
                        {
                            // We need to preserve the casing of the escape characters in the audience,
                            // so defer decoding the URL until later.
                            parsedFields.Add(fieldParts[0], fieldParts[1]);
                        }
                        else
                        {
                            parsedFields.Add(fieldParts[0], WWW.UnEscapeURL(fieldParts[1]));
                        }
                    }
                }

                return parsedFields;
            }
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

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Text;    

    /// <summary>
    /// This abstract base class can be extended to implement additional token providers.
    /// </summary>
    public abstract class TokenProvider
    {
        internal static readonly TimeSpan DefaultTokenTimeout = TimeSpan.FromMinutes(60);
        internal static readonly Func<string, byte[]> MessagingTokenProviderKeyEncoder = Encoding.UTF8.GetBytes;
        const TokenScope DefaultTokenScope = TokenScope.Entity;

        /// <summary></summary>
        protected TokenProvider()
            : this(TokenProvider.DefaultTokenScope)
        {
        }

        /// <summary></summary>
        /// <param name="tokenScope"></param>
        protected TokenProvider(TokenScope tokenScope)
        {
            this.TokenScope = tokenScope;
            this.ThisLock = new object();
        }

        /// <summary>
        /// Gets the scope or permissions associated with the token.
        /// </summary>
        public TokenScope TokenScope { get; set; }

        /// <summary></summary>
        protected object ThisLock { get; set;  }

        /// <summary>
        /// Construct a TokenProvider based on a sharedAccessSignature.
        /// </summary>
        /// <param name="sharedAccessSignature">The shared access signature</param>
        /// <returns>A TokenProvider initialized with the shared access signature</returns>
        public static TokenProvider CreateSharedAccessSignatureTokenProvider(string sharedAccessSignature)
        {
            return new SharedAccessSignatureTokenProvider(sharedAccessSignature);
        }

        /// <summary>
        /// Construct a TokenProvider based on the provided Key Name and Shared Access Key.
        /// </summary>
        /// <param name="keyName">The key name of the corresponding SharedAccessKeyAuthorizationRule.</param>
        /// <param name="sharedAccessKey">The key associated with the SharedAccessKeyAuthorizationRule</param>
        /// <returns>A TokenProvider initialized with the provided RuleId and Password</returns>
        public static TokenProvider CreateSharedAccessSignatureTokenProvider(string keyName, string sharedAccessKey)
        {
            return new SharedAccessSignatureTokenProvider(keyName, sharedAccessKey, DefaultTokenTimeout);
        }

        ////internal static TokenProvider CreateIoTTokenProvider(string keyName, string sharedAccessKey)
        ////{
        ////    return new IoTTokenProvider(keyName, sharedAccessKey, DefaultTokenTimeout);
        ////}

        /// <summary>
        /// Construct a TokenProvider based on the provided Key Name and Shared Access Key.
        /// </summary>
        /// <param name="keyName">The key name of the corresponding SharedAccessKeyAuthorizationRule.</param>
        /// <param name="sharedAccessKey">The key associated with the SharedAccessKeyAuthorizationRule</param>
        /// <param name="tokenTimeToLive">The token time to live</param>
        /// <returns>A TokenProvider initialized with the provided RuleId and Password</returns>
        public static TokenProvider CreateSharedAccessSignatureTokenProvider(string keyName, string sharedAccessKey, TimeSpan tokenTimeToLive)
        {
            return new SharedAccessSignatureTokenProvider(keyName, sharedAccessKey, tokenTimeToLive);
        }

        /// <summary>
        /// Construct a TokenProvider based on the provided Key Name and Shared Access Key.
        /// </summary>
        /// <param name="keyName">The key name of the corresponding SharedAccessKeyAuthorizationRule.</param>
        /// <param name="sharedAccessKey">The key associated with the SharedAccessKeyAuthorizationRule</param>
        /// <param name="tokenScope">The tokenScope of tokens to request.</param>
        /// <returns>A TokenProvider initialized with the provided RuleId and Password</returns>
        public static TokenProvider CreateSharedAccessSignatureTokenProvider(string keyName, string sharedAccessKey, TokenScope tokenScope)
        {
            return new SharedAccessSignatureTokenProvider(keyName, sharedAccessKey, DefaultTokenTimeout, tokenScope);
        }

        /// <summary>
        /// Construct a TokenProvider based on the provided Key Name and Shared Access Key.
        /// </summary>
        /// <param name="keyName">The key name of the corresponding SharedAccessKeyAuthorizationRule.</param>
        /// <param name="sharedAccessKey">The key associated with the SharedAccessKeyAuthorizationRule</param>
        /// <param name="tokenTimeToLive">The token time to live</param>
        /// <param name="tokenScope">The tokenScope of tokens to request.</param>
        /// <returns>A TokenProvider initialized with the provided RuleId and Password</returns>
        public static TokenProvider CreateSharedAccessSignatureTokenProvider(string keyName, string sharedAccessKey, TimeSpan tokenTimeToLive, TokenScope tokenScope)
        {
            return new SharedAccessSignatureTokenProvider(keyName, sharedAccessKey, tokenTimeToLive, tokenScope);
        }

        /// <summary>
        /// Gets a <see cref="SecurityToken"/> for the given audience and duration.
        /// </summary>
        /// <param name="appliesTo">The URI which the access token applies to</param>
        /// <param name="action">The request action</param>
        /// <param name="timeout">The time span that specifies the timeout value for the message that gets the security token</param>
        /// <returns></returns>
        public SecurityToken GetToken(string appliesTo, string action, TimeSpan timeout)
        {
            TimeoutHelper.ThrowIfNegativeArgument(timeout);
            appliesTo = this.NormalizeAppliesTo(appliesTo);
            return this.OnGetToken(appliesTo, action, timeout);
        }

        /// <summary></summary>
        /// <param name="appliesTo"></param>
        /// <param name="action"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        protected abstract SecurityToken OnGetToken(string appliesTo, string action, TimeSpan timeout);

        /// <summary></summary>
        /// <param name="appliesTo"></param>
        /// <returns></returns>
        protected virtual string NormalizeAppliesTo(string appliesTo)
        {
            return ServiceBusUriHelper.NormalizeUri(appliesTo, "http", true, stripPath: this.TokenScope == TokenScope.Namespace, ensureTrailingSlash: true);
        }
    }
}

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;

    /// <summary>
    /// Provides information about a security token such as audience, expiry time, and the string token value.
    /// </summary>
    public class SecurityToken
    {
        // per Simple Web Token draft specification
        private const string TokenAudience = "Audience";
        private const string TokenExpiresOn = "ExpiresOn";
        private const string TokenIssuer = "Issuer";
        private const string TokenDigest256 = "HMACSHA256";

        const string InternalExpiresOnFieldName = "ExpiresOn";
        const string InternalAudienceFieldName = TokenAudience;
        const string InternalKeyValueSeparator = "=";
        const string InternalPairSeparator = "&";
        static readonly Func<string, string> Decoder = WWW.UnEscapeURL;
        static readonly DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        readonly string token;
        readonly DateTime expiresAtUtc;
        readonly string audience;

        /// <summary>
        /// Creates a new instance of the <see cref="SecurityToken"/> class.
        /// </summary>
        /// <param name="tokenString">The token</param>
        /// <param name="expiresAtUtc">The expiration time</param>
        /// <param name="audience">The audience</param>
        public SecurityToken(string tokenString, DateTime expiresAtUtc, string audience)
        {
            if (tokenString == null || audience == null)
            {
                throw Fx.Exception.ArgumentNull(tokenString == null ? "tokenString" : "audience");
            }

            this.token = tokenString;
            this.expiresAtUtc = expiresAtUtc;
            this.audience = audience;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SecurityToken"/> class.
        /// </summary>
        /// <param name="tokenString">The token</param>
        /// <param name="expiresAtUtc">The expiration time</param>
        public SecurityToken(string tokenString, DateTime expiresAtUtc)
        {
            if (tokenString == null)
            {
                throw Fx.Exception.ArgumentNull("tokenString");
            }

            this.token = tokenString;
            this.expiresAtUtc = expiresAtUtc;
            this.audience = this.GetAudienceFromToken(tokenString);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SecurityToken"/> class.
        /// </summary>
        /// <param name="tokenString">The token</param>
        public SecurityToken(string tokenString)
        {
            if (tokenString == null)
            {
                throw Fx.Exception.ArgumentNull("tokenString");
            }

            this.token = tokenString;
            this.GetExpirationDateAndAudienceFromToken(tokenString, out this.expiresAtUtc, out this.audience);
        }

        /// <summary>
        /// Gets the audience of this token.
        /// </summary>
        public string Audience
        {
            get
            {
                return this.audience;
            }
        }

        /// <summary>
        /// Gets the expiration time of this token.
        /// </summary>
        public DateTime ExpiresAtUtc
        {
            get
            {
                return this.expiresAtUtc;
            }
        }

        /// <summary>
        /// Gets the actual token.
        /// </summary>
        public string TokenValue
        {
            get { return this.token; }
        }

        /// <summary></summary>
        protected virtual string ExpiresOnFieldName
        {
            get
            {
                return InternalExpiresOnFieldName;
            }
        }

        /// <summary></summary>
        protected virtual string AudienceFieldName
        {
            get
            {
                return InternalAudienceFieldName;
            }
        }

        /// <summary></summary>
        protected virtual string KeyValueSeparator
        {
            get
            {
                return InternalKeyValueSeparator;
            }
        }

        /// <summary></summary>
        protected virtual string PairSeparator
        {
            get
            {
                return InternalPairSeparator;
            }
        }

        static IDictionary<string, string> Decode(string encodedString, Func<string, string> keyDecoder, Func<string, string> valueDecoder, string keyValueSeparator, string pairSeparator)
        {
            IDictionary<string, string> dictionary = new Dictionary<string, string>();
            IEnumerable<string> valueEncodedPairs = encodedString.Split(new[] { pairSeparator }, StringSplitOptions.None);
            foreach (string valueEncodedPair in valueEncodedPairs)
            {
                string[] pair = valueEncodedPair.Split(new[] { keyValueSeparator }, StringSplitOptions.None);
                if (pair.Length != 2)
                {
                    throw new FormatException(Resources.InvalidEncoding);
                }

                dictionary.Add(keyDecoder(pair[0]), valueDecoder(pair[1]));
            }

            return dictionary;
        }

        string GetAudienceFromToken(string token)
        {
            string audience;
            IDictionary<string, string> decodedToken = Decode(token, Decoder, Decoder, this.KeyValueSeparator, this.PairSeparator);
            if (!decodedToken.TryGetValue(this.AudienceFieldName, out audience))
            {
                throw new FormatException(Resources.TokenMissingAudience);
            }

            return audience;
        }

        void GetExpirationDateAndAudienceFromToken(string token, out DateTime expiresOn, out string audience)
        {
            string expiresIn;
            IDictionary<string, string> decodedToken = Decode(token, Decoder, Decoder, this.KeyValueSeparator, this.PairSeparator);
            if (!decodedToken.TryGetValue(this.ExpiresOnFieldName, out expiresIn))
            {
                throw new FormatException(Resources.TokenMissingExpiresOn);
            }

            if (!decodedToken.TryGetValue(this.AudienceFieldName, out audience))
            {
                throw new FormatException(Resources.TokenMissingAudience);
            }

            expiresOn = (EpochTime + TimeSpan.FromSeconds(double.Parse(expiresIn, CultureInfo.InvariantCulture)));
        }
    }
}

namespace Microsoft.Azure.ServiceBus
{
    using System;

    static class ServiceBusUriHelper
    {
        internal static string NormalizeUri(string uri, string scheme, bool stripQueryParameters = true, bool stripPath = false, bool ensureTrailingSlash = false)
        {
            UriBuilder uriBuilder = new UriBuilder(uri)
            {
                Scheme = scheme,
                Port = -1,
                Fragment = string.Empty,
                Password = string.Empty,
                UserName = string.Empty,
            };

            if (stripPath)
            {
                uriBuilder.Path = string.Empty;
            }

            if (stripQueryParameters)
            {
                uriBuilder.Query = string.Empty;
            }

            if (ensureTrailingSlash)
            {
                if (!uriBuilder.Path.EndsWith("/", StringComparison.Ordinal))
                {
                    uriBuilder.Path += "/";
                }
            }

            return uriBuilder.Uri.AbsoluteUri;
        }
    }
}

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    [DebuggerStepThrough]
    struct TimeoutHelper
    {
        public static readonly TimeSpan MaxWait = TimeSpan.FromMilliseconds(int.MaxValue);
        DateTime deadline;
        bool deadlineSet;
        TimeSpan originalTimeout;

        public TimeoutHelper(TimeSpan timeout)
            : this(timeout, false)
        {
        }

        public TimeoutHelper(TimeSpan timeout, bool startTimeout)
        {
            Fx.Assert(timeout >= TimeSpan.Zero, "timeout must be non-negative");

            this.originalTimeout = timeout;
            this.deadline = DateTime.MaxValue;
            this.deadlineSet = (timeout == TimeSpan.MaxValue);

            if (startTimeout && !this.deadlineSet)
            {
                this.SetDeadline();
            }
        }

        public TimeSpan OriginalTimeout
        {
            get { return this.originalTimeout; }
        }

        public static bool IsTooLarge(TimeSpan timeout)
        {
            return (timeout > TimeoutHelper.MaxWait) && (timeout != TimeSpan.MaxValue);
        }

        public static TimeSpan FromMilliseconds(int milliseconds)
        {
            if (milliseconds == Timeout.Infinite)
            {
                return TimeSpan.MaxValue;
            }

            return TimeSpan.FromMilliseconds(milliseconds);
        }

        public static int ToMilliseconds(TimeSpan timeout)
        {
            if (timeout == TimeSpan.MaxValue)
            {
                return Timeout.Infinite;
            }

            long ticks = Ticks.FromTimeSpan(timeout);
            if (ticks / TimeSpan.TicksPerMillisecond > int.MaxValue)
            {
                return int.MaxValue;
            }
            return Ticks.ToMilliseconds(ticks);
        }

        public static TimeSpan Min(TimeSpan val1, TimeSpan val2)
        {
            if (val1 > val2)
            {
                return val2;
            }

            return val1;
        }

        public static DateTime Min(DateTime val1, DateTime val2)
        {
            if (val1 > val2)
            {
                return val2;
            }

            return val1;
        }

        public static TimeSpan Add(TimeSpan timeout1, TimeSpan timeout2)
        {
            return Ticks.ToTimeSpan(Ticks.Add(Ticks.FromTimeSpan(timeout1), Ticks.FromTimeSpan(timeout2)));
        }

        public static DateTime Add(DateTime time, TimeSpan timeout)
        {
            if (timeout >= TimeSpan.Zero && DateTime.MaxValue - time <= timeout)
            {
                return DateTime.MaxValue;
            }
            if (timeout <= TimeSpan.Zero && DateTime.MinValue - time >= timeout)
            {
                return DateTime.MinValue;
            }
            return time + timeout;
        }

        public static DateTime Subtract(DateTime time, TimeSpan timeout)
        {
            return Add(time, TimeSpan.Zero - timeout);
        }

        public static TimeSpan Divide(TimeSpan timeout, int factor)
        {
            if (timeout == TimeSpan.MaxValue)
            {
                return TimeSpan.MaxValue;
            }

            return Ticks.ToTimeSpan((Ticks.FromTimeSpan(timeout) / factor) + 1);
        }

        public static void ThrowIfNegativeArgument(TimeSpan timeout)
        {
            ThrowIfNegativeArgument(timeout, "timeout");
        }

        public static void ThrowIfNegativeArgument(TimeSpan timeout, string argumentName)
        {
            if (timeout < TimeSpan.Zero)
            {
                throw Fx.Exception.ArgumentOutOfRange(argumentName, timeout, Resources.TimeoutMustBeNonNegative.FormatForUser(argumentName, timeout));
            }
        }

        public static void ThrowIfNonPositiveArgument(TimeSpan timeout)
        {
            ThrowIfNonPositiveArgument(timeout, "timeout");
        }

        public static void ThrowIfNonPositiveArgument(TimeSpan timeout, string argumentName)
        {
            if (timeout <= TimeSpan.Zero)
            {
                throw Fx.Exception.ArgumentOutOfRange(argumentName, timeout, Resources.TimeoutMustBePositive.FormatForUser(argumentName, timeout));
            }
        }

        public static bool WaitOne(WaitHandle waitHandle, TimeSpan timeout)
        {
            ThrowIfNegativeArgument(timeout);
            if (timeout == TimeSpan.MaxValue)
            {
                waitHandle.WaitOne();
                return true;
            }

            return waitHandle.WaitOne(timeout);
        }

        public TimeSpan RemainingTime()
        {
            if (!this.deadlineSet)
            {
                this.SetDeadline();
                return this.originalTimeout;
            }

            if (this.deadline == DateTime.MaxValue)
            {
                return TimeSpan.MaxValue;
            }

            TimeSpan remaining = this.deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            return remaining;
        }

        public TimeSpan ElapsedTime()
        {
            return this.originalTimeout - this.RemainingTime();
        }

        void SetDeadline()
        {
            Fx.Assert(!this.deadlineSet, "TimeoutHelper deadline set twice.");
            this.deadline = DateTime.UtcNow + this.originalTimeout;
            this.deadlineSet = true;
        }
    }
}

namespace Microsoft.Azure.ServiceBus
{
    using System;

    static class Ticks
    {
        public static long Now
        {
            get
            {
                long time = DateTime.UtcNow.ToFileTimeUtc();
                return time;
            }
        }

        public static long FromMilliseconds(int milliseconds)
        {
            return checked(milliseconds * TimeSpan.TicksPerMillisecond);
        }

        public static int ToMilliseconds(long ticks)
        {
            return checked((int)(ticks / TimeSpan.TicksPerMillisecond));
        }

        public static long FromTimeSpan(TimeSpan duration)
        {
            return duration.Ticks;
        }

        public static TimeSpan ToTimeSpan(long ticks)
        {
            return new TimeSpan(ticks);
        }

        public static long Add(long firstTicks, long secondTicks)
        {
            if (firstTicks == long.MaxValue || firstTicks == long.MinValue)
            {
                return firstTicks;
            }

            if (secondTicks == long.MaxValue || secondTicks == long.MinValue)
            {
                return secondTicks;
            }

            if (firstTicks >= 0 && long.MaxValue - firstTicks <= secondTicks)
            {
                return long.MaxValue - 1;
            }

            if (firstTicks <= 0 && long.MinValue - firstTicks >= secondTicks)
            {
                return long.MinValue + 1;
            }

            return checked(firstTicks + secondTicks);
        }
    }
}

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Diagnostics;

    static class Fx
    {
        static ExceptionUtility exceptionUtility;

        public static ExceptionUtility Exception
        {
            get
            {
                if (exceptionUtility == null)
                {
                    exceptionUtility = new ExceptionUtility();
                }

                return exceptionUtility;
            }
        }

        [Conditional("DEBUG")]
        public static void Assert(bool condition, string message)
        {
            Debug.Assert(condition, message);
        }

        public static class Tag
        {
            public enum CacheAttrition
            {
                None,
                ElementOnTimer,

                // A finalizer/WeakReference based cache, where the elements are held by WeakReferences (or hold an
                // inner object by a WeakReference), and the weakly-referenced object has a finalizer which cleans the
                // item from the cache.
                ElementOnGC,

                // A cache that provides a per-element token, delegate, interface, or other piece of context that can
                // be used to remove the element (such as IDisposable).
                ElementOnCallback,

                FullPurgeOnTimer,
                FullPurgeOnEachAccess,
                PartialPurgeOnTimer,
                PartialPurgeOnEachAccess,
            }

            public enum Location
            {
                InProcess,
                OutOfProcess,
                LocalSystem,
                LocalOrRemoteSystem, // as in a file that might live on a share
                RemoteSystem,
            }

            public enum SynchronizationKind
            {
                LockStatement,
                MonitorWait,
                MonitorExplicit,
                InterlockedNoSpin,
                InterlockedWithSpin,

                // Same as LockStatement if the field type is object.
                FromFieldType,
            }

            [Flags]
            public enum BlocksUsing
            {
                MonitorEnter,
                MonitorWait,
                ManualResetEvent,
                AutoResetEvent,
                AsyncResult,
                IAsyncResult,
                PInvoke,
                InputQueue,
                ThreadNeutralSemaphore,
                PrivatePrimitive,
                OtherInternalPrimitive,
                OtherFrameworkPrimitive,
                OtherInterop,
                Other,

                NonBlocking, // For use by non-blocking SynchronizationPrimitives such as IOThreadScheduler
            }

            public static class Strings
            {
                internal const string ExternallyManaged = "externally managed";
                internal const string AppDomain = "AppDomain";
                internal const string DeclaringInstance = "instance of declaring class";
                internal const string Unbounded = "unbounded";
                internal const string Infinite = "infinite";
            }

            [AttributeUsage(
                AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Constructor,
                AllowMultiple = true,
                Inherited = false)]
            [Conditional("CODE_ANALYSIS")]
            public sealed class ExternalResourceAttribute : Attribute
            {
                readonly Location location;
                readonly string description;

                public ExternalResourceAttribute(Location location, string description)
                {
                    this.location = location;
                    this.description = description;
                }

                public Location Location
                {
                    get
                    {
                        return this.location;
                    }
                }

                public string Description
                {
                    get
                    {
                        return this.description;
                    }
                }
            }

            [AttributeUsage(AttributeTargets.Field)]
            [Conditional("CODE_ANALYSIS")]
            public sealed class CacheAttribute : Attribute
            {
                readonly Type elementType;
                readonly CacheAttrition cacheAttrition;

                public CacheAttribute(Type elementType, CacheAttrition cacheAttrition)
                {
                    this.Scope = Strings.DeclaringInstance;
                    this.SizeLimit = Strings.Unbounded;
                    this.Timeout = Strings.Infinite;

                    if (elementType == null)
                    {
                        throw Fx.Exception.ArgumentNull("elementType");
                    }

                    this.elementType = elementType;
                    this.cacheAttrition = cacheAttrition;
                }

                public Type ElementType
                {
                    get
                    {
                        return this.elementType;
                    }
                }

                public CacheAttrition CacheAttrition
                {
                    get
                    {
                        return this.cacheAttrition;
                    }
                }

                public string Scope { get; set; }

                public string SizeLimit { get; set; }

                public string Timeout { get; set; }
            }

            [AttributeUsage(AttributeTargets.Field)]
            [Conditional("CODE_ANALYSIS")]
            public sealed class QueueAttribute : Attribute
            {
                readonly Type elementType;

                public QueueAttribute(Type elementType)
                {
                    this.Scope = Strings.DeclaringInstance;
                    this.SizeLimit = Strings.Unbounded;

                    if (elementType == null)
                    {
                        throw Fx.Exception.ArgumentNull("elementType");
                    }

                    this.elementType = elementType;
                }

                public Type ElementType
                {
                    get
                    {
                        return this.elementType;
                    }
                }

                public string Scope { get; set; }

                public string SizeLimit { get; set; }

                public bool StaleElementsRemovedImmediately { get; set; }

                public bool EnqueueThrowsIfFull { get; set; }
            }

            // Set on a class when that class uses lock (this) - acts as though it were on a field
            //     object this;
            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class, Inherited = false)]
            [Conditional("CODE_ANALYSIS")]
            public sealed class SynchronizationObjectAttribute : Attribute
            {
                public SynchronizationObjectAttribute()
                {
                    this.Blocking = true;
                    this.Scope = Strings.DeclaringInstance;
                    this.Kind = SynchronizationKind.FromFieldType;
                }

                public bool Blocking { get; set; }

                public string Scope { get; set; }

                public SynchronizationKind Kind { get; set; }
            }

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
            [Conditional("CODE_ANALYSIS")]
            public sealed class SynchronizationPrimitiveAttribute : Attribute
            {
                readonly BlocksUsing blocksUsing;

                public SynchronizationPrimitiveAttribute(BlocksUsing blocksUsing)
                {
                    this.blocksUsing = blocksUsing;
                }

                public BlocksUsing BlocksUsing
                {
                    get
                    {
                        return this.blocksUsing;
                    }
                }

                public bool SupportsAsync { get; set; }

                public bool Spins { get; set; }

                public string ReleaseMethod { get; set; }

                [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false)]
                [Conditional("CODE_ANALYSIS")]
                public sealed class BlockingAttribute : Attribute
                {
                    public string CancelMethod { get; set; }

                    public Type CancelDeclaringType { get; set; }

                    public string Conditional { get; set; }
                }

                // Sometime a method will call a conditionally-blocking method in such a way that it is guaranteed
                // not to block (i.e. the condition can be Asserted false).  Such a method can be marked as
                // GuaranteeNonBlocking as an assertion that the method doesn't block despite calling a blocking method.
                //
                // Methods that don't call blocking methods and aren't marked as Blocking are assumed not to block, so
                // they do not require this attribute.
                [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false)]
                [Conditional("CODE_ANALYSIS")]
                public sealed class GuaranteeNonBlockingAttribute : Attribute
                {
                    public GuaranteeNonBlockingAttribute()
                    {
                    }
                }

                [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false)]
                [Conditional("CODE_ANALYSIS")]
                public sealed class NonThrowingAttribute : Attribute
                {
                    public NonThrowingAttribute()
                    {
                    }
                }

                [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = true, Inherited = false)]
                [Conditional("CODE_ANALYSIS")]
                public class ThrowsAttribute : Attribute
                {
                    readonly Type exceptionType;
                    readonly string diagnosis;

                    public ThrowsAttribute(Type exceptionType, string diagnosis)
                    {
                        if (exceptionType == null)
                        {
                            throw Fx.Exception.ArgumentNull("exceptionType");
                        }
                        if (string.IsNullOrEmpty(diagnosis))
                        {
                            ////throw Fx.Exception.ArgumentNullOrEmpty("diagnosis");
                            throw new ArgumentNullException("diagnosis");
                        }

                        this.exceptionType = exceptionType;
                        this.diagnosis = diagnosis;
                    }

                    public Type ExceptionType
                    {
                        get
                        {
                            return this.exceptionType;
                        }
                    }

                    public string Diagnosis
                    {
                        get
                        {
                            return this.diagnosis;
                        }
                    }
                }

                [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false)]
                [Conditional("CODE_ANALYSIS")]
                public sealed class InheritThrowsAttribute : Attribute
                {
                    public InheritThrowsAttribute()
                    {
                    }

                    public Type FromDeclaringType { get; set; }

                    public string From { get; set; }
                }

                [AttributeUsage(
                    AttributeTargets.Assembly | AttributeTargets.Module | AttributeTargets.Class |
                    AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Constructor | AttributeTargets.Method |
                    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Interface |
                    AttributeTargets.Delegate, AllowMultiple = false,
                    Inherited = false)]
                [Conditional("CODE_ANALYSIS")]
                public sealed class SecurityNoteAttribute : Attribute
                {
                    public SecurityNoteAttribute()
                    {
                    }

                    public string Critical { get; set; }

                    public string Safe { get; set; }

                    public string Miscellaneous { get; set; }
                }
            }
        }
    }
}

namespace Microsoft.Azure.ServiceBus
{
    using System;

    class ExceptionUtility
    {
        internal ExceptionUtility()
        {
        }

        public ArgumentException Argument(string paramName, string message)
        {
            return new ArgumentException(message, paramName);
        }

        public Exception ArgumentNull(string paramName)
        {
            return new ArgumentNullException(paramName);
        }

        public ArgumentException ArgumentNullOrWhiteSpace(string paramName)
        {
            return this.Argument(paramName, Resources.ArgumentNullOrWhiteSpace.FormatForUser(paramName));
        }

        public ArgumentOutOfRangeException ArgumentOutOfRange(string paramName, object actualValue, string message)
        {
            return new ArgumentOutOfRangeException(paramName, actualValue, message);
        }

        public Exception AsError(Exception exception)
        {
            return exception;
        }
    }

    class Resources
    {
        public static string ArgumentNullOrWhiteSpace = "";

        public static string TimeoutMustBeNonNegative = "";
        public static string TimeoutMustBePositive = "";

        public static string ArgumentStringTooBig = "";
        public static string InvalidEncoding = "";
        public static string TokenMissingAudience = "";
        public static string TokenMissingExpiresOn = "";
    }

    static class Ext
    {
        public static string FormatForUser(this string value, string param, TimeSpan param2)
        {
            return value;
        }

        public static string FormatForUser(this string value, string param, int param2)
        {
            return value;
        }

        public static string FormatForUser(this string value, string param)
        {
            return value;
        }
    }
}

namespace Microsoft.ServiceBus
{
    /// <summary>Enumerates the token scope for the service bus.</summary>
    public enum TokenScope
    {
        Namespace,
        Entity,
    }
}