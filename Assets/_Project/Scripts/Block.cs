using System;
using System.Linq;
using _Framework.Scripts.Extensions;
using UnityEngine;

namespace _Project.Scripts
{
    public class Block : MonoBehaviour
    {
        private BlockClone blockClone;
        private bool snapActive;

        private (Transform thisSocket, Transform otherSocket)[] GetConnections()
        {
            var sockets = transform.GetComponentsInChildren<Socket>();
            var connected = new Socket[sockets.Length];
            
            // Find all connected pairs
            for (var i = 0; i < sockets.Length; i++)
            {
                var socket = sockets[i];
                var candidate = socket.GetComponent<Socket>().Trigger();
                if (candidate.IsEmpty() == false)
                {
                    var closest = candidate.OrderBy(o => o.transform.position.Distance(socket.transform.position)).First();
                    var sockets2 = closest;
                    if (sockets2.Type != socket.Type)
                    {
                        connected[i] = closest;
                    }
                }
            }

            // Sort connection for the closest one
            return sockets.Select((socket, i) =>
                {
                    if (connected[i] == null) return null;
                    var dist = sockets[i].transform.position.Distance(connected[i].transform.position);
                    return new {Index = i, Distance = dist};
                })
                .Where(it => it != null)
                .OrderBy(arg => arg.Distance)
                .Select(arg => (sockets[arg.Index].transform, connected[arg.Index].transform))
                .ToArray();
        }
        
        private void FixedUpdate()
        {
            var connections = GetConnections();

            if (snapActive && connections.Length >= 2)
            {
                blockClone.gameObject.SetActive(true);
                
                // Chose two closes connections and choose origin and alignment.
                var thisSocketA = connections[0].thisSocket;
                var otherSocketA = connections[0].otherSocket;
                var thisSocketB = connections[1].thisSocket;
                var otherSocketB = connections[1].otherSocket;

                var thisDir = (thisSocketB.position - thisSocketA.position).normalized;
                var otherDir = (otherSocketB.position - otherSocketA.position).normalized;

                var thisToOtherRotation = Quaternion.FromToRotation(thisDir, otherDir);
                
                // Correct for rotation along the direction (multiple valid states for resulting rotation)
                var correctedUpVector = thisToOtherRotation * -thisSocketA.up;
                var angle = Vector3.SignedAngle(correctedUpVector, otherSocketA.up, otherDir);
                var correction = Quaternion.AngleAxis(angle, otherDir);
                
                var targetRotation = correction * thisToOtherRotation *  transform.rotation;

//                var socketWorld = thisSocketA.transform.position;
//                var socketLocalToBlock = transform.InverseTransformPoint(socketWorld);
//                this.socketWorld = transform.TransformPoint(correction * thisToOtherRotation * socketLocalToBlock);
//                var dif = otherSocketA.transform.position - this.socketWorld;
//                
//                blockClone.transform.rotation = targetRotation;
//                blockClone.transform.position = transform.position + dif;

                var cloneTransform = blockClone.transform;
                cloneTransform.rotation = targetRotation;
                cloneTransform.position = transform.position;
                var adjustedPosition = cloneTransform.TransformPoint(transform.InverseTransformPoint(thisSocketA.position));
                var offset = otherSocketA.position - adjustedPosition;
                cloneTransform.position += offset;
            }
            else
            {
                blockClone.gameObject.SetActive(false);
            }
        }

        public void BeginSnap()
        {
            snapActive = true;
        }

        public void EndSnap()
        {
            var result = blockClone.GetLock();

            if (result.BlockA != null && result.BlockB != null)
            {
                ConnectBlocks(result);
            }

            snapActive = false;
        }

        private void ConnectBlocks(LockResult result)
        {
            foreach (var socket in result.BlockASockets.Concat(result.BlockBSockets))
            {
                Destroy(socket.gameObject);
            }

            var blockA = result.BlockA;
            var blockB = result.BlockB;
            
            print($"Connecting {blockA.name} to {blockB.name}");
            
            blockA.transform.CopyWorldFrom(blockClone.transform);
            var fixedJoint = blockA.gameObject.AddComponent<FixedJoint>();
            fixedJoint.connectedBody = blockB.GetComponent<Rigidbody>();

            blockA.Connect(blockB);

            var blockLinks = blockA.GetAllLinks();
            var connectedToGround = blockLinks.Any(l => l.GetComponent<Rigidbody>().isKinematic);
            if (connectedToGround)
            {
                foreach (var link in blockLinks)
                {
                    if (link.GetComponent<Rigidbody>().isKinematic == false)
                    {
                        var block = link.GetComponent<Block>();
                        Destroy(block.blockClone.gameObject);
                        Destroy(block.GetComponent<BlockGrab>());
                        Destroy(block);
                    }
                }
            }
        }

        private void Awake()
        {
            blockClone = new GameObject().AddComponent<BlockClone>();
            blockClone.Setup(this);
        }

        private void OnDrawGizmosSelected()
        {
            var connections = GetConnections();
            for (var i = 0; i < connections.Length; i++)
            {
                var color = Color.white;
                var size = 0.01f;
                if (i == 0) color = Color.red;
                if (i == 1) color = Color.blue;
                if (i >= 2) size = 0.005f;
                
                var from = connections[i].thisSocket.transform.position;
                var to = connections[i].otherSocket.transform.position;
                
                Gizmos.color = color.SetAlpha(.5f);
                Gizmos.DrawSphere(from, size);
                Gizmos.DrawSphere(to, size);
                Gizmos.DrawLine(from, to);
            }
        }
    }
}