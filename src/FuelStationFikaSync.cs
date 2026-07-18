using System;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using UnityEngine;

namespace FuelStationRefill
{
    /// <summary>
    /// Manages Fika network synchronization for fuel station state.
    /// Host randomizes fuel at raid start, broadcasts to all clients.
    /// Clients send consume requests; host updates and rebroadcasts.
    /// </summary>
    public class FuelStationFikaSync : IDisposable
    {
        private readonly PluginCore _plugin;
        private bool _subscribed;
        private bool _packetsRegistered;

        public FuelStationFikaSync(PluginCore plugin)
        {
            _plugin = plugin;
        }

        public void Initialize()
        {
            try
            {
                FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(OnNetworkManagerCreated);
                FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerDestroyedEvent>(OnNetworkManagerDestroyed);
                _subscribed = true;

                // Try to register immediately if manager already exists
                TryRegisterPackets();

                PluginCore.Log.LogInfo("[FuelFika] Initialized");
            }
            catch (Exception ex)
            {
                PluginCore.Log.LogWarning($"[FuelFika] Init error (Fika not installed?): {ex.Message}");
            }
        }

        private void OnNetworkManagerCreated(FikaNetworkManagerCreatedEvent evt)
        {
            try
            {
                _packetsRegistered = false;
                TryRegisterPackets();

                // Host: randomize and broadcast fuel state on raid start
                if (_plugin.IsFikaHost())
                {
                    _plugin.RandomizeFuelForAllZones();
                    BroadcastFuelState();
                    PluginCore.Log.LogInfo("[FuelFika] Host: randomized and broadcasted fuel state");
                }
            }
            catch (Exception ex)
            {
                PluginCore.Log.LogError($"[FuelFika] OnNetworkManagerCreated error: {ex.Message}");
            }
        }

        private void OnNetworkManagerDestroyed(FikaNetworkManagerDestroyedEvent evt)
        {
            _packetsRegistered = false;
            PluginCore.Log.LogInfo("[FuelFika] NetworkManager destroyed");
        }

        private void TryRegisterPackets()
        {
            if (_packetsRegistered) return;

            var nm = _plugin.GetFikaNetworkManager();
            if (nm == null) return;

            try
            {
                nm.RegisterPacket<FuelStationSyncPacket>(OnSyncPacketReceived);
                nm.RegisterPacket<FuelStationConsumePacket>(OnConsumePacketReceived);
                _packetsRegistered = true;
                PluginCore.Log.LogInfo("[FuelFika] Packets registered");
            }
            catch (Exception ex)
            {
                PluginCore.Log.LogError($"[FuelFika] RegisterPackets error: {ex.Message}");
            }
        }

        /// <summary>
        /// Host broadcasts full fuel state to all clients.
        /// </summary>
        public void BroadcastFuelState()
        {
            if (!_packetsRegistered) return;

            try
            {
                _plugin.GetFuelStateArrays(out float[] currents, out float[] maxs, out bool[] active);
                var packet = new FuelStationSyncPacket
                {
                    ZoneCount = currents.Length,
                    CurrentFuels = currents,
                    MaxFuels = maxs,
                    IsActive = active
                };

                SendPacket(packet, broadcast: true);
                PluginCore.Log.LogInfo($"[FuelFika] Broadcasted fuel state for {currents.Length} zones");
            }
            catch (Exception ex)
            {
                PluginCore.Log.LogError($"[FuelFika] BroadcastFuelState error: {ex.Message}");
            }
        }

        /// <summary>
        /// Client sends consume request to host.
        /// </summary>
        public void SendConsumeRequest(int zoneIndex, float amountConsumed)
        {
            if (!_packetsRegistered) return;

            try
            {
                var packet = new FuelStationConsumePacket
                {
                    ZoneIndex = zoneIndex,
                    AmountConsumed = amountConsumed
                };

                SendPacket(packet, broadcast: false);
                PluginCore.Log.LogInfo($"[FuelFika] Sent consume: zone={zoneIndex} amount={amountConsumed:F1}");
            }
            catch (Exception ex)
            {
                PluginCore.Log.LogError($"[FuelFika] SendConsumeRequest error: {ex.Message}");
            }
        }

        private void SendPacket<T>(T packet, bool broadcast) where T : INetSerializable
        {
            var nm = _plugin.GetFikaNetworkManager();
            if (nm == null) return;

            if (_plugin.IsFikaHost())
            {
                var server = Comfort.Common.Singleton<FikaServer>.Instance;
                if (server != null)
                {
                    server.SendData(ref packet, DeliveryMethod.ReliableOrdered, broadcast: broadcast);
                }
            }
            else
            {
                var client = Comfort.Common.Singleton<FikaClient>.Instance;
                if (client != null)
                {
                    client.SendData(ref packet, DeliveryMethod.ReliableOrdered);
                }
            }
        }

        private void OnSyncPacketReceived(FuelStationSyncPacket packet)
        {
            try
            {
                // Only accept sync from host (ignore if we are host ourselves)
                if (_plugin.IsFikaHost()) return;

                _plugin.ApplySyncData(packet.CurrentFuels, packet.MaxFuels, packet.IsActive);
                PluginCore.Log.LogInfo($"[FuelFika] Received sync for {packet.ZoneCount} zones");
            }
            catch (Exception ex)
            {
                PluginCore.Log.LogError($"[FuelFika] OnSyncPacketReceived error: {ex.Message}");
            }
        }

        private void OnConsumePacketReceived(FuelStationConsumePacket packet)
        {
            try
            {
                // Only host processes consume requests
                if (!_plugin.IsFikaHost()) return;

                _plugin.GetFuelStateArrays(out float[] currents, out float[] maxs, out bool[] active);

                if (packet.ZoneIndex >= 0 && packet.ZoneIndex < currents.Length)
                {
                    currents[packet.ZoneIndex] -= packet.AmountConsumed;
                    if (currents[packet.ZoneIndex] <= 0f)
                    {
                        currents[packet.ZoneIndex] = 0f;
                        active[packet.ZoneIndex] = false;
                    }

                    // Apply locally on host
                    _plugin.ApplySyncData(currents, maxs, active);

                    // Re-broadcast updated state to all clients
                    BroadcastFuelState();
                    PluginCore.Log.LogInfo($"[FuelFika] Host processed consume: zone={packet.ZoneIndex} consumed={packet.AmountConsumed:F1} remaining={currents[packet.ZoneIndex]:F0}");
                }
            }
            catch (Exception ex)
            {
                PluginCore.Log.LogError($"[FuelFika] OnConsumePacketReceived error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_subscribed)
            {
                try
                {
                    FikaEventDispatcher.UnsubscribeEvent<FikaNetworkManagerCreatedEvent>(OnNetworkManagerCreated);
                    FikaEventDispatcher.UnsubscribeEvent<FikaNetworkManagerDestroyedEvent>(OnNetworkManagerDestroyed);
                }
                catch { }
                _subscribed = false;
            }
            _packetsRegistered = false;
        }
    }
}
