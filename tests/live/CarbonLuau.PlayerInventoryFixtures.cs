// Isolated C1 fixture. The connection is controlled, not an authenticated client.
using System;
using UnityEngine;
namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        partial void RunPlayerInventoryFixtures() { NextTick(RunC1InventoryFixture); }
        private void RunC1InventoryFixture()
        {
            const ulong Id=76561198000000114;
            BasePlayer Player=null; bool Passed=false;
            try {
                if(BasePlayer.activePlayerList.Count!=0 || Host==null || !Host.Ready) throw new Exception("isolated ready server required");
                Player=GameManager.server.CreateEntity("assets/prefabs/player/player.prefab",new Vector3(0,-100,0)) as BasePlayer;
                if(Player==null) throw new Exception("controlled Player creation failed");
                Player.enableSaving=false; Player.userID=Id; Player.UserIDString=Id.ToString(); Player.displayName="C1 fixture";
                Player.Spawn(); Player.lifestate=BaseCombatEntity.LifeState.Alive;
                Player.net.connection=new Network.Connection {userid=Id,username=Player.displayName,connected=true,active=true,player=Player};
                BasePlayer.activePlayerLookup[Id]=Player; BasePlayer.activePlayerList.Add(Player); OnPlayerConnected(Player);
                if(Addons!=null){Addons.Dispose();Addons=null;} Host.Dispose();
                string Source="local S=require('state'); S.P=game:GetService('Players'):GetPlayerByUserId('"+Id+"'); S.I=S.P:GetInventory(); assert(S.I==S.P:GetInventory())";
                Host=new ScriptHost(Native,new RuntimeConfig {MaxCallbackMilliseconds=100},()=>{
                    var Snapshot=new ScriptSnapshot {EntryName="init.luau",EntrySource=Source};
                    Snapshot.Modules.Add("state","return {}");
                    Snapshot.Modules.Add("bad","require('state').Leaked=require('state').P:GetInventory(); error('failed module')");return Snapshot;
                },Gameplay);
                if(Host.Reload().Status!=RuntimeStatus.OK) throw new Exception("C1 root load");
                Action<string> Run=Code=>{var Result=Host.Execute("c1.live",Code);if(Result.Status!=RuntimeStatus.OK)throw new Exception(Result.Error);};
                Run("local S=require('state'); assert(S.I:CountItem('scrap')==S.P:CountItem('scrap')); assert(not S.I:HasItem('c1.unknown'))");
                Run("local S=require('state'); assert(S.P:GiveItem('scrap',3)); assert(S.I:CountItem('scrap')==3); assert(S.I:HasItem('scrap') and S.I:HasItem('scrap',3) and not S.I:HasItem('scrap',4)); assert(S.P:TakeItem('scrap',1)); assert(S.I:CountItem('scrap')==2)");
                Run("local S=require('state'); assert(not pcall(function() require('bad') end)); assert(not pcall(function() S.Leaked:CountItem('scrap') end)); assert(S.I:CountItem('scrap')==2); assert(not pcall(function() S.I.Amount=1 end)); assert(not pcall(function() return S.I.ItemAdded end))");
                Source="error('failed root')"; if(Host.Reload().Status!=RuntimeStatus.RUNTIME_ERROR) throw new Exception("failed candidate");
                Run("assert(require('state').I:CountItem('scrap')==2)");
                OnPlayerDisconnected(Player,"controlled C1 disconnect");
                Run("local S=require('state'); assert(not pcall(function() S.I:CountItem('scrap') end)); assert(not pcall(function() S.P:GetInventory() end))");
                Player.net.connection=new Network.Connection {userid=Id,username=Player.displayName,connected=true,active=true,player=Player}; OnPlayerConnected(Player);
                Run("local S=require('state'); local P=game:GetService('Players'):GetPlayerByUserId('"+Id+"'); assert(P~=S.P); assert(not pcall(function() S.I:HasItem('scrap') end)); assert(P:GetInventory():CountItem('scrap')==2)");
                Source="local S=require('state'); S.I=game:GetService('Players'):GetPlayerByUserId('"+Id+"'):GetInventory()";
                if(Host.Reload().Status!=RuntimeStatus.OK)throw new Exception("root replacement");
                if(Host.Execute("c1.fatal","while true do end").Status!=RuntimeStatus.TIMEOUT || !Host.Ready)throw new Exception("recovery");
                Run("assert(require('state').I:CountItem('scrap')==2)");
                Passed=true;
            } catch(Exception Error) { PrintError("[CarbonLuau:PlayerInventoryFixture] FAIL "+Error); }
            finally {
                ReleaseNative();
                if(Player!=null){OnPlayerDisconnected(Player,"C1 cleanup");BasePlayer.activePlayerLookup.Remove(Id);BasePlayer.activePlayerList.Remove(Player);if(Player.net!=null)Player.net.connection=null;Player.Kill();}
                ItemManager.DoRemoves();
                if(Passed) Puts("[CarbonLuau:PlayerInventoryFixture] PASS actual physical inventory, controlled connection, Give/Take parity, publication, reconnect and recovery; no authenticated-client claim");
                ConsoleSystem.Run(ConsoleSystem.Option.Server,"quit");
            }
        }
    }
}
