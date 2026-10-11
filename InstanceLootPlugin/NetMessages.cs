using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace InstanceLootPlugin
{
    /// <summary>
    /// Server -> all clients. Tells each client to create its own local CommandCube.
    /// </summary>
    public struct SpawnInstancedCubeMessage : INetMessage
    {
        private UniquePickup pickup;
        private Vector3 position;

        public SpawnInstancedCubeMessage(UniquePickup pickup, Vector3 position)
        {
            this.pickup = pickup;
            this.position = position;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(position);
            writer.Write(in pickup);
        }

        public void Deserialize(NetworkReader reader)
        {
            position = reader.ReadVector3();
            pickup = reader.ReadUniquePickup();
        }

        public void OnReceived() => InstancedCommandCube.Create(pickup, position);
    }

    /// <summary>
    /// Client -> server. Spawns the pickup a player chose from their local CommandCube. Clients can't spawn network
    /// objects, so the server does it.
    /// </summary>
    public struct ClaimInstancedPickupMessage : INetMessage
    {
        private UniquePickup pickup;
        private Vector3 position;

        public ClaimInstancedPickupMessage(UniquePickup pickup, Vector3 position)
        {
            this.pickup = pickup;
            this.position = position;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(position);
            writer.Write(in pickup);
        }

        public void Deserialize(NetworkReader reader)
        {
            position = reader.ReadVector3();
            pickup = reader.ReadUniquePickup();
        }

        public void OnReceived()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            Log.Debug($"Creating claimed pickup {pickup.pickupIndex}");
            GenericPickupController.CreatePickup(new GenericPickupController.CreatePickupInfo
            {
                pickup = pickup,
                position = position,
                rotation = Quaternion.identity
            });
        }
    }
}
