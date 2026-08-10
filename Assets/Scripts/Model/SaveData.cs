using System;
using System.Collections.Generic;
using UnityEngine;
///这个是需要保存的数据
[Serializable]
public class SaveData
{
    //显示在存档位置
    public string _detalTime;//存档时间
    public int _displayHp;//当前生命值
    public int _displayMaxHp;//最大生命值
    public string _floorName;//当前楼层

    //角色属性,战斗
    public int _currentHealth;
    public int _maxHealth;
    public int _attackPower;
    public float _attackRange;
    public int _defensePower;

    //角色实体属性
    public float _playerPosX;
    public float _playerPosY;
    public float _moveSpeed;

    //地图
    public int _mapWidth;
    public int _mapHeight;
    public int[] _tileGrid;
    public List<RoomData> _room;

    //敌人数据
    public List<EnemySaveData> _enemyData;

    //背包
    public List<string> _packageData;

}

//敌人数据
[Serializable]
public class EnemySaveData
{
    public int _emenId;
    public int _enemCurrentHp;
    public int _enemMaxHp;
    public int _attackPower;
    public int _defensePower;
    public float _attackRange;
    public float _chaseRange;
    public float _moveSpeed;
    public float _attackDuration;
    public float _hitCheckTime;
    public float _hurtDuration;
    public float _knockbackForce;
    public float _knockbackDecay;
    public int _state;
    public float _posX;
    public float _posY;
    public int _facingDir;
    public bool _isDead;
}

