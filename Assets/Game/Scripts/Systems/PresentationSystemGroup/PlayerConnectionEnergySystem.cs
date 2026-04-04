using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UniRx;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Zenject;
[DisableAutoCreation]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(GridUpdateSystem))]
public partial class PlayerConnectionEnergySystem : SystemBase
{
    [Inject] PlayerPlaceBuildingSystem _buildingSystem;
    [Inject] IReadOnlyBuildingInfo _buildingInfo;
    [Inject] ConnectEnergyFactory _connectFactory;
    [Inject] GameController _gameController;
    [Inject] GameFieldSettings _gamefield;
    Entity _playerState;
    IPlayerConnectData _playerConnectData;
    EntityQuery _buildReadyQuery;
    EnergyNode _connectTo;
    EnergyNode _connectFrom;
    
    public  Action onActionDone;
    int2 _connectFromData;
    int2 _connectToData;
    Action<bool,bool> placeAction;
    Action backAction;
    int Type;
    EnergyLine energyLine;
    
    Dictionary<int,BuildingEnegryConfig> buildingEnegryConfigs;
    public void SetUpBuilding(ConnectType connectType,IPlayerConnectData playerConnectData,Entity playerState)
    {
        if(energyLine==null) 
            energyLine=_connectFactory.CreateLine();
        if(EntityManager.IsComponentEnabled<PlayerPlacingBuilding>(playerState)||EntityManager.IsComponentEnabled<PlayerPlacingRoad>(playerState)||EntityManager.IsComponentEnabled<PlayerDeletePoints>(playerState)) return;
        
        _playerConnectData=playerConnectData;
        _playerState=playerState;
        _connectFromData=new int2(-1,-1);
        _connectToData=new int2(-1,-1);
        EntityManager.SetComponentEnabled<PlayerConnectBuildings>(playerState,true);
        if (connectType == ConnectType.EnergyDisconnect)
        {
            
            Type=-1;
        }
        else
        {
            buildingEnegryConfigs=_buildingInfo.BuildingEnegryConfigs.Where(f=>f.Value.BuildingID!="Core") .ToDictionary(f => f.Key, f => f.Value);
            Type=((int)connectType)-1;
        }
    }

    protected override void OnCreate()
    {
        _buildReadyQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<PlayerCommand>()
        .WithAll<PlayerConnectBuildings>()
        .WithDisabled<PlayerPlacingRoad>()
        .WithDisabled<PlayerDeletePoints>()
        .WithDisabled<PathfindingRequest>()
        
        .Build(this);
        RequireForUpdate(_buildReadyQuery);
    }
    protected override void OnUpdate()
    {
        
        var entitiesDictionary= SystemAPI.GetSingleton<EntitiesDictionary>();
        
        if (!_buildReadyQuery.IsEmpty && _connectFromData.y != -1)
        {
            if (_connectToData.y == -1)
            {
                if(_playerConnectData.energyNode!=null&&_playerConnectData.energyNode.nodeData.y!=_connectFromData.y)
                {
                    _connectToData=_playerConnectData.energyNode.nodeData;
                    _connectTo=_playerConnectData.energyNode;
                }
                
            }
            else
            {
                _connectTo=_buildingSystem.energyNode;
            }
            if (_connectFrom == null)
            {
                if(!entitiesDictionary.Entities.ContainsKey(_connectFromData.y)) return;
                var fromEn=entitiesDictionary.Entities[_connectFromData.y];
                _connectFrom=(EntityManager.GetComponentData<BuildingOnSceneReference>(fromEn).buildingOnScene as EnergyBuildingOnScene).nodes[_connectFromData.x];
            }
            if (_connectTo == null&&entitiesDictionary.Entities.ContainsKey(_connectToData.y))
            {
                var fromEn=entitiesDictionary.Entities[_connectToData.y];
                _connectTo=(EntityManager.GetComponentData<BuildingOnSceneReference>(fromEn).buildingOnScene as EnergyBuildingOnScene).nodes[_connectToData.x];
            }
            Vector3 to=_connectTo==null?_playerConnectData.posV3:_connectTo.Connect.transform.position;
            _connectFactory.Modifyine(energyLine,_connectFrom.Connect.transform.position,to,Type!=-1&&Vector3.Distance(_connectFrom.Connect.transform.position,to)<_gamefield.range);
        }

    } 
    public void Rotate(bool isHold)
    {
        if(isHold)
        {
            if (_connectFromData.y == -1||Type==-1) return;
            if(Type!=0)
                _buildingSystem?.Back();
            Type++;
            Type=Type % (buildingEnegryConfigs.Count+1) ;
            
            if (Type== 0)
            {
                placeAction=ConnectNodes;
                backAction=BackForLine;
                _connectTo=null;
                _connectToData=new int2(-1,-1);
            }
            else
            {
                _buildingSystem.SetUpBuilding( buildingEnegryConfigs.Keys.ElementAt(Type-1),_playerConnectData,_playerState,_connectFromData);
                _connectTo=_buildingSystem.energyNode;
                _connectToData=_buildingSystem.connectTo;
                placeAction=PlaceBuilding;
                backAction=BackForBuilding;
            }
        }
    }
    void ConnectNodes(bool isHold,bool IsBlueprint)
    {
        var ecb = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
        if (_connectFromData.y!=-1&&_connectToData.y!=-1)
        {
            var command=ecb.CreateEntity();
            var buff = ecb.AddBuffer<LinkNetworkEnergyTo>(command);
            buff.Add(new LinkNetworkEnergyTo{LinkFromBuilding=_connectFromData,LinkToBuilding=_connectToData});
            _connectFactory.ResetLine(energyLine,Vector3.zero);
        }
    }
    void DisConnectNodes(bool isHold,bool IsBlueprint)
    {
        if (_connectFromData.y!=-1&&_connectToData.y!=-1)
        {
            var ecb = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>().CreateCommandBuffer();
            var command=ecb.CreateEntity();
            var buff = ecb.AddBuffer<UnLinkNetworkEnergyTo>(command);
            buff.Add(new UnLinkNetworkEnergyTo{UnLinkFromBuilding=_connectFromData,UnLinkToBuilding=_connectToData});
            _connectFactory.Disconnect(_connectFrom);
            _connectFactory.Disconnect(_connectTo);
            _connectFactory.ResetLine(_connectFrom);
            _connectFactory.ResetLine(_connectTo);
            _connectFactory.ResetLine(energyLine,Vector3.zero);
        }
    }
    void PlaceBuilding(bool isHold,bool IsBlueprint)
    {
        _buildingSystem.PlaceBuilding(isHold,IsBlueprint);
        _connectToData=_buildingSystem.connectTo;
        ConnectNodes(isHold,IsBlueprint);
        _connectFromData=_buildingSystem.NextConnectFrom;
        _connectFrom=null;
    }
    void BackForLine()
    {
        
        if(_connectFromData.y != -1)
        {
            _connectFactory.ResetLine(energyLine,Vector3.zero);
            _connectFromData=new int2(-1,-1);
            _connectFrom=null;
            _connectTo=null;
        }
        else 
            onActionDone?.Invoke();
    }
    void BackForBuilding()
    {
        if(_connectFromData.y != -1)
        {
            _connectFactory.ResetLine(energyLine,Vector3.zero);
            _connectFromData=new int2(-1,-1);
            _connectFrom=null;
            _buildingSystem?.Back();
        }
        else
        {
            onActionDone?.Invoke();
        }
    }
    public void ConnectBuildings(bool isHold,bool IsBlueprint)
    {
        if (_connectFromData.y == -1)
        { 
            if(_playerConnectData.energyNode==null) return;
            _connectFromData=_playerConnectData.energyNode.nodeData;
            _connectFrom=_playerConnectData.energyNode;
            if (Type == -1)
            {
                Debug.Log("sdssd");
                placeAction=DisConnectNodes;
                backAction=BackForLine;
                _connectTo=null;
                _connectToData=new int2(-1,-1);
            }
            else
            {
                Type=Type % (buildingEnegryConfigs.Count+1) ;
                if (Type== 0)
                {
                    placeAction=ConnectNodes;
                    backAction=BackForLine;
                    _connectTo=null;
                    
                    _connectToData=new int2(-1,-1);
                }
                else
                {
                    Debug.Log(Type-1);
                    Debug.Log(buildingEnegryConfigs.Keys.ElementAt(Type-1));
                    
                    _buildingSystem.SetUpBuilding( buildingEnegryConfigs.Keys.ElementAt(Type-1),_playerConnectData,_playerState,_connectFromData);
                    _connectTo=_buildingSystem.energyNode;
                    _connectToData=_buildingSystem.connectTo;
                    placeAction=PlaceBuilding;
                    backAction=BackForBuilding;
                }
            }
        }
        else
        {
            placeAction?.Invoke(isHold,IsBlueprint);
            if (!isHold)
            {
                _connectFromData=new int2(-1,-1);
                Back();
            }
        }
    }
    public void Back()
    {
        backAction?.Invoke();
    }
    protected override void OnDestroy()
    {
        GameObject.DestroyImmediate(energyLine);
    }
}
public enum ConnectType
{
    EnergyConnectNode=1,
    EnergyСonnectAndCreateLEP=2,
    EnergyConnectAndCreateConc=3,
    EnergyDisconnect=4,
}