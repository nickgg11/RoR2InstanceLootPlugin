using RoR2;
using RoR2.Artifacts;
using RoR2.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace InstanceLootPlugin
{
    
    public class SpawnCustomController : NetworkBehaviour
    {
        /// <summary>
        /// Per Client handle that Receives messages when items are dropped and creates an item locally.
        /// </summary>
        [NetworkMessageHandler(msgType = 969, client = true)]
        public static void HandlePickupMessage(NetworkMessage netMsg)
        {
            SpawnCustomMessage pickupMessage = new SpawnCustomMessage();
            netMsg.ReadMessage<SpawnCustomMessage>(pickupMessage);

            Log.Info($"HandlePickupMessage {pickupMessage.pickupIndex.value}");

            CreatePickupDroplet(pickupMessage.pickupIndex, pickupMessage.position);

        }

        public static void CreatePickupDroplet(PickupIndex pickupIndex, Vector3 position)
        {
            var pickupInfo = new GenericPickupController.CreatePickupInfo
            {
                rotation = Quaternion.identity,
                _pickupState = new UniquePickup(pickupIndex)
            };
      
            GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(CommandArtifactManager.commandCubePrefab, position, pickupInfo.rotation);
            var pickupIndexNetworker = gameObject.GetComponent<PickupIndexNetworker>();
            var picker = gameObject.GetComponent<PickupPickerController>();

            picker.SetOptionsInternal(PickupPickerController.GetOptionsFromPickupState((UniquePickup)pickupInfo._pickupState));

            if (!NetworkServer.active)
            {
                picker.SetDirtyBit(PickupPickerController.optionsDirtyBit);
            }

            pickupIndexNetworker.NetworkpickupState = (UniquePickup)pickupInfo._pickupState;
            if (pickupIndexNetworker.pickupDisplay)
            {
                pickupIndexNetworker.pickupDisplay.SetPickup((UniquePickup)pickupInfo._pickupState, false);
            }
        }

        /// <summary>
        ///  Creates item, impl is from GenericPickupController.CreatePickup
        /// </summary>
        /// <param name="pickupIndex"></param>
        /// <param name="position"></param>
        public static void CreatePickupItem(PickupIndex pickupIndex, Vector3 position)
        {
            if (!NetworkServer.active)
            {
                return;
            }

            var pickupInfo = new GenericPickupController.CreatePickupInfo
            {
                rotation = Quaternion.identity,
                position = position,
                _pickupState = new UniquePickup(pickupIndex),
            };

            GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(pickupInfo.prefabOverride ?? GenericPickupController.pickupPrefab, position, pickupInfo.rotation);
            GenericPickupController component = gameObject.GetComponent<GenericPickupController>();
            if (component)
            {
                GenericPickupController genericPickupController = component;
                GenericPickupController.CreatePickupInfo createPickupInfo2 = pickupInfo;
                genericPickupController.Network_pickupState = (UniquePickup)createPickupInfo2._pickupState;
            }
            PickupIndexNetworker component2 = gameObject.GetComponent<PickupIndexNetworker>();
            if (component2)
            {
                PickupIndexNetworker pickupIndexNetworker = component2;
                GenericPickupController.CreatePickupInfo createPickupInfo2 = pickupInfo;
                pickupIndexNetworker.NetworkpickupState = (UniquePickup)createPickupInfo2._pickupState;
            }
            PickupPickerController component3 = gameObject.GetComponent<PickupPickerController>();
            if (component3 && pickupInfo.pickerOptions != null)
            {
                component3.SetOptionsServer(pickupInfo.pickerOptions);
            }

            NetworkServer.Spawn(gameObject);
        }
    }
}
