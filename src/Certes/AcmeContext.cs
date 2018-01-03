﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Certes.Acme;
using Certes.Acme.Resource;
using Certes.Jws;
using AuthorizationIdentifier = Certes.Acme.Resource.AuthorizationIdentifier;
using Certificate = Certes.Acme.Resource.Certificate;

namespace Certes
{
    /// <summary>
    /// Represents the context for ACME operations.
    /// </summary>
    /// <seealso cref="Certes.IAcmeContext" />
    public class AcmeContext : IAcmeContext
    {
        private Directory directory;
        private IAccountContext accountContext;
        private Uri accountLocation;

        /// <summary>
        /// Initializes a new instance of the <see cref="AcmeContext" /> class.
        /// </summary>
        /// <param name="directoryUri">The directory URI.</param>
        /// <param name="accountKey">The account key.</param>
        /// <param name="http">The HTTP client.</param>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="directoryUri"/> is <c>null</c>.
        /// </exception>
        public AcmeContext(Uri directoryUri, IAccountKey accountKey = null, IAcmeHttpClient http = null)
        {
            DirectoryUri = directoryUri ?? throw new ArgumentNullException(nameof(directoryUri));
            AccountKey = accountKey ?? new AccountKey();
            HttpClient = http ?? new AcmeHttpClient(directoryUri);
        }

        /// <summary>
        /// Gets the account.
        /// </summary>
        /// <value>
        /// The account.
        /// </value>
        /// <exception cref="NotImplementedException"></exception>
        public IAccountContext Account => accountContext ?? (accountContext = new AccountContext(this));

        /// <summary>
        /// Gets the ACME HTTP client.
        /// </summary>
        /// <value>
        /// The ACME HTTP client.
        /// </value>
        /// <exception cref="NotImplementedException"></exception>
        public IAcmeHttpClient HttpClient { get; }

        /// <summary>
        /// Gets the directory URI.
        /// </summary>
        /// <value>
        /// The directory URI.
        /// </value>
        public Uri DirectoryUri { get; }

        /// <summary>
        /// Gets the account key.
        /// </summary>
        /// <value>
        /// The account key.
        /// </value>
        public IAccountKey AccountKey { get; private set; }

        /// <summary>
        /// Changes the key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task ChangeKey(AccountKey key = null)
        {
            var endpoint = await this.GetResourceUri(d => d.KeyChange);
            var location = await this.GetAccountLocation();
            
            var newKey = key ?? new AccountKey();
            var keyChange = new
            {
                account = location,
                newKey = newKey.JsonWebKey
            };

            var jws = new JwsSigner(newKey);
            var body = jws.Sign(keyChange, url: endpoint);

            var payload = await Sign(body, endpoint);
            await HttpClient.Post<Account>(endpoint, payload, true);

            AccountKey = newKey;
        }

        /// <summary>
        /// Creates the account.
        /// </summary>
        /// <returns>
        /// The account created.
        /// </returns>
        public async Task<Account> NewAccount(IList<string> contact, bool termsOfServiceAgreed = false)
        {
            var body = new Account
            {
                Contact = contact,
                TermsOfServiceAgreed = termsOfServiceAgreed
            };

            var resp = await NewAccount(body, true);
            this.accountLocation = resp.Location;
            return resp.Resource;
        }

        /// <summary>
        /// Gets the ACME directory.
        /// </summary>
        /// <returns>
        /// The ACME directory.
        /// </returns>
        public async Task<Directory> GetDirectory()
        {
            if (directory == null)
            {
                var resp = await HttpClient.Get<Directory>(DirectoryUri);
                directory = resp.Resource;
            }

            return directory;
        }

        /// <summary>
        /// Revokes the certificate.
        /// </summary>
        /// <returns>
        /// The awaitable.
        /// </returns>
        /// <exception cref="NotImplementedException"></exception>
        public Task RevokeCertificate()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Signs the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="uri">The URI.</param>
        /// <returns></returns>
        public async Task<JwsPayload> Sign(object entity, Uri uri)
        {
            var nonce = await HttpClient.ConsumeNonce();
            var location = await this.GetAccountLocation();
            var jws = new JwsSigner(AccountKey);
            return jws.Sign(entity, location, uri, nonce);
        }

        /// <summary>
        /// Gets the account location.
        /// </summary>
        /// <returns></returns>
        public async Task<Uri> GetAccountLocation()
        {
            if (accountLocation != null)
            {
                return accountLocation;
            }
            
            var resp = await NewAccount(new Account { OnlyReturnExisting = true }, false);

            return resp.Location;
        }

        private async Task<AcmeHttpResponse<Account>> NewAccount(Account body, bool ensureSuccessStatusCode)
        {
            var endpoint = await this.GetResourceUri(d => d.NewAccount);
            var jws = new JwsSigner(AccountKey);
            var payload = jws.Sign(body, url: endpoint, nonce: await HttpClient.ConsumeNonce());
            return await HttpClient.Post<Account>(endpoint, payload, ensureSuccessStatusCode);
        }

        /// <summary>
        /// Create a bew the order.
        /// </summary>
        /// <param name="identifiers">The identifiers.</param>
        /// <param name="notBefore">The not before.</param>
        /// <param name="notAfter">The not after.</param>
        /// <returns>
        /// TODO
        /// </returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<IOrderContext> NewOrder(IList<string> identifiers, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
        {
            var endpoint = await this.GetResourceUri(d => d.NewOrder);

            var body = new Certificate
            {
                Identifiers = identifiers
                    .Select(id => new AuthorizationIdentifier { Type = "dns", Value = id })
                    .ToArray(),
                NotBefore = notBefore,
                NotAfter = notAfter,
            };

            var payload = await Sign(body, endpoint);
            var order = await HttpClient.Post<Order>(endpoint, payload, true);
            return new OrderContext(this, order.Location);
        }
    }
}