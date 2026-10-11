using System.Collections.Generic;
using RoR2;
using RoR2.Artifacts;
using UnityEngine;
using UnityEngine.Networking;

namespace InstanceLootPlugin
{
    /// <summary>
    /// Marks a CommandCube that only exists on this client. These are created from <see cref="SpawnInstancedCubeMessage"/>
    /// and are never spawned on the network, so the server doesn't know about them.
    /// </summary>
    public class InstancedCommandCube : MonoBehaviour
    {
        public static readonly List<InstancedCommandCube> Instances = new List<InstancedCommandCube>();

        private void OnEnable() => Instances.Add(this);

        private void OnDisable() => Instances.Remove(this);

        public static void Create(UniquePickup pickup, Vector3 position)
        {
            GameObject prefab = CommandArtifactManager.commandCubePrefab;
            if (!prefab)
            {
                Log.Warning("Command cube prefab is not loaded, can't create instanced cube.");
                return;
            }

            GameObject cube = Instantiate(prefab, position, Quaternion.identity);
            cube.AddComponent<InstancedCommandCube>();

            var picker = cube.GetComponent<PickupPickerController>();
            picker.SetOptionsInternal(PickupPickerController.GetOptionsFromPickupState(pickup));

            var pickupIndexNetworker = cube.GetComponent<PickupIndexNetworker>();
            pickupIndexNetworker.NetworkpickupState = pickup;
            if (pickupIndexNetworker.pickupDisplay)
            {
                pickupIndexNetworker.pickupDisplay.SetPickup(pickup, false);
            }
        }
    }

    /// <summary>
    /// Added on the server to droplets whose CommandCube should be instanced per player instead of spawned on the network.
    /// </summary>
    public class InstancedDropletMarker : MonoBehaviour
    {
        /// <summary>
        /// The only player who gets a cube for this droplet, for rewards the game drops once per player.
        /// Null means every player gets one.
        /// </summary>
        public NetworkConnection owner;

        /// <summary>
        /// This droplet is the only one kept from a reward the game drops once per player.
        /// </summary>
        public bool replacesPlayerPile;
    }
}
