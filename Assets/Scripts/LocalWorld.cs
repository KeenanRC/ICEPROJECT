﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

class LevelSyncMessage : MessageBase
{
    public string message;
    
    public Vector3 playerPosition;
    public float   visibleRadius;
}

class BlockAddMessage : MessageBase
{
    public float px;
    public float pz;
}

/// A local level block represents a component of the environment
/// in which the player exists. The complete environment will consist
/// of several of these blocks, and any other environmentally global
/// components.
/// This class allow interrogation of the local world structure, as 
/// recently updated from the master copy on the server. It also matches
/// this to GameObjects in the scene holding geometry and other attributes.
public class LocalLevelBlock
{
    public RegionBlock region;
    public GameObject  gobject;
}

public class LocalWorld : NetworkBehaviour {

    /// The game object that will be the base object for each
    /// local level block. Presumably an empty.
    public GameObject localLevelElement;
    
    /// The game object used to represent a brick.
    public GameObject localBrick;
    
    /// Define the distance about the player that we
    /// are interested in seeing things.
    private float viewRadius;
    
    private List<LocalLevelBlock> levelStructure; 
    
    private bool foundPlayer;
    
    // Use this for initialization
    void Start () {
        viewRadius = 15.0f;
        
        levelStructure = new List<LocalLevelBlock> ();
        
        foundPlayer = false;
        
        Debug.Log ("Local world level instance started");
        
    }
    
    /// Identify the level block corresponding to a particular position.
    /// May return null if no such block is cached locally.
    LocalLevelBlock findLevelBlock (Vector3 position)
    {
        foreach (LocalLevelBlock i in levelStructure)
        {
            if (i.region.contains (position.x, position.z))
            {
                return i;
            }
        }
        return null;
    }
    
    /// Create a level block corresponding to the given region block and
    /// add this to the cache of blocks. Assumes that such a block does
    /// not already exist, or a duplicate will be created.
    LocalLevelBlock addLevelBlock (RegionBlock rb, Vector3 position)
    {
        Debug.Log ("New local block " + position);
        LocalLevelBlock llb = new LocalLevelBlock ();
        llb.region = rb;
        llb.gobject = Object.Instantiate (localLevelElement, position, Quaternion.identity);
        levelStructure.Add (llb);
        return llb;
    }                    
    
    /// Remove any cached region blocks that are outside the visible region.
    void flushRegions ()
    {
        NetworkManager nm = NetworkManager.singleton;
        if (nm.client != null)
        {
            PlayerController player = ClientScene.localPlayers[0];
            
            Vector3 playerPosition = player.gameObject.transform.position;
            
            for (int i = levelStructure.Count - 1; i >= 0; i--)
            {
                if (!levelStructure[i].region.contains (playerPosition.x, playerPosition.z, viewRadius))
                {
                    UnityEngine.Object.Destroy (levelStructure[i].gobject);
                    levelStructure.RemoveAt (i);
                }
            }
        }
        
    }
    
    public override void OnStartClient()
    {
        Debug.Log ("On Client start " + NetworkClient.allClients);
        
        NetworkClient.allClients[0].RegisterHandler (LevelMsgType.LevelResponse, ServerCommandHandler);
    }
    
    // Update is called once per frame
    void Update () {
        // Check if the player has been created, and associate with that player if so.
        if (!foundPlayer)
        {
            if (ClientScene.localPlayers.Count > 0)
            {
                PlayerMove player = ClientScene.localPlayers[0].gameObject.GetComponent <PlayerMove>();
                player.setLocalWorld (this);
                foundPlayer = true;
            }
        }
        
        //         Debug.Log ("Update");
        NetworkManager nm = NetworkManager.singleton;
        if (ClientScene.localPlayers.Count > 0)
        {
            PlayerController player = ClientScene.localPlayers[0];
            
            if (nm.client != null)
            {
                LevelSyncMessage m = new LevelSyncMessage ();
                m.message = "Hello World!";
                m.playerPosition = player.gameObject.transform.position;
                m.visibleRadius = viewRadius;
                
                NetworkManager.singleton.client.Send (LevelMsgType.LevelRequest, m);
            }
        }
    }
    
    void ServerCommandHandler (NetworkMessage netMsg)
    {
        switch (netMsg.msgType)
        {
            case LevelMsgType.LevelResponse:
            {
                RegionBlock rb = netMsg.ReadMessage<RegionBlock>();
                Debug.Log ("Server Got message: " + rb.blockSize);
                
                MeshFilter mf = GetComponent <MeshFilter>();
                rb.convertToMesh (mf.mesh);        
                
                Vector2 rbpos = rb.getPosition ();
                Vector3 llbpos = new Vector3 (rbpos.x, 0.0f, rbpos.y);
                LocalLevelBlock llb = findLevelBlock (llbpos);
                if (llb == null)
                {
                    llb = addLevelBlock (rb, llbpos);
                    
                    // llb should now be valid.
                    llb.region.placeBlocks (localBrick, llb.gobject.transform);
                }
                else
                {
                    // if version is newer than the one we already have, then update it.
                    if (rb.timeLastChanged > llb.region.timeLastChanged)
                    {
                        llb.region = rb;
                        Debug.Log ("Got update ..................................>");
                        llb.region.placeBlocks (localBrick, llb.gobject.transform);
                    }
                }
                
                flushRegions ();
            }
            break;
            default:
            {
                Debug.Log ("Unexpected message type in LocalWorld");                
            }
            break;
        }
    }
    
    // Add a new block at the given position.
    public void placeBlock (float x, float z)
    {
        Vector3 llbpos = new Vector3 (x, 0.0f, z);
        LocalLevelBlock llb = findLevelBlock (llbpos);
        if (llb != null)
        {
            Vector3 regionpos = new Vector3 (x - llb.region.blockCoordX, z - llb.region.blockCoordY);
            // llb should now be valid.
//            llb.region.placeSingleBlock (localBrick, regionpos, llb.gobject.transform);
        }
        else
        // else no region - potential problem.
        {
            Debug.Log ("No level block at " + llbpos);
        }
        
        
        BlockAddMessage m = new BlockAddMessage ();
        m.px = x;
        m.pz = z;
        NetworkManager.singleton.client.Send (LevelMsgType.LevelUpdate, m);        
    }
}
