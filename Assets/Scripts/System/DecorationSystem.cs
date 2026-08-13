using System.Collections.Generic;
using UnityEngine;

public interface IDecorationSystem : ISystem
{

}

public class DecorationSystem : AbstractSystem, IDecorationSystem
{
    protected override void OnInit()
    {
        this.RegisterEvent<MapGeneratedEvent>(OnMapGenerated);
    }

    private void OnMapGenerated(MapGeneratedEvent e)
    {
        SpawnDecorationNoColletion();
    }

    void SpawnDecorationNoColletion()
    {
        var map = this.GetModel<IMapModel>();
        var door = this.GetModel<IDoorModel>();
        var spawn = this.GetUtility<ISpawnUtility>();

        //加载
        var configs = Resources.LoadAll<DecorationConfig>("Perfabs/Decoration");
        if (configs.Length == 0) return;

        float totalWeight = 0;
        foreach (var item in configs) totalWeight += item.weight;

        //收集已有
        var occupied = new HashSet<Vector2Int>();
        if (door != null)
        {
            foreach (var item in door.Doors)
            {
                occupied.Add(item.Position);
            }
        }

        foreach (var item in map.Rooms)
        {
            int area = item.Width * item.Height;
            int count = Mathf.FloorToInt(area * Random.Range(0.15f, 0.25f));//随机数量

            for (int i = 0; i < count; i++)
            {
                if (!TryPickTile(map, item, occupied, out int gx, out int gy))
                {
                    continue;
                }

                var config = PickWeighted(configs, totalWeight);
                if (config == null) continue;

                var pos = new Vector2(gx + 0.5f, gy + 0.5f);//中心位置
                spawn.SpawnDecoration(config.prefab, pos);

                occupied.Add(new Vector2Int(gx, gy));
            }
        }
    }

    //可放置的格子
    bool TryPickTile(IMapModel map, RoomData room, HashSet<Vector2Int> blocked, out int gx, out int gy)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            gx = Random.Range(room.X, room.X + room.Width);//随机位置
            gy = Random.Range(room.Y, room.Y + room.Height);

            if (gx < 0 || gx >= map.Width || gy < 0 || gy >= map.Height)
                continue;

            if (map.TileGrid[gx, gy] != 1) continue;//不是墙

            if (blocked.Contains(new Vector2Int(gx, gy))) continue;

            int cx = (int)room.Center.x;
            int cy = (int)room.Center.y;
            if (gx >= cx - 1 && gx <= cx && gy >= cy - 1 && gy <= cy)
                continue;
            return true;
        }
        gx = gy = 0;
        return false;
    }

    //根据权重随机选择
    DecorationConfig PickWeighted(DecorationConfig[] configs, float totalWeight)
    {
        float roll = Random.Range(0f, totalWeight);
        float acc = 0f;
        foreach (var c in configs)
        {
            acc += c.weight;
            if (roll <= acc) return c;
        }
        return configs[configs.Length - 1];
    }
}
