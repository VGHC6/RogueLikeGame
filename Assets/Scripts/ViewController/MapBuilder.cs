using UnityEngine;
using UnityEngine.Tilemaps;

public class MapBuilder : MonoBehaviour, IController
{
    [SerializeField] private Tilemap _floorTileMap;
    [SerializeField] private Tilemap _wallTileMap;
    [SerializeField] private TileBase _floorTile;
    [SerializeField] private TileBase _wallTile;

    [Header("边缘（单方向邻海）")]
    [SerializeField] private TileBase _edgeTop;
    [SerializeField] private TileBase _edgeBottom;
    [SerializeField] private TileBase _edgeLeft;
    [SerializeField] private TileBase _edgeRight;

    [Header("角落（两方向邻海）")]
    [SerializeField] private TileBase _cornerTL;
    [SerializeField] private TileBase _cornerTR;
    [SerializeField] private TileBase _cornerBL;
    [SerializeField] private TileBase _cornerBR;

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    // 方向：上 下 左 右
    static readonly Vector2Int[] Dirs =
    {
        new(0, 1),
        new(0, -1),
        new(-1, 0),
        new(1, 0),
    };

    void Awake()
    {
        // 水渲染在陆地下层，防止海岸线被遮盖
        _wallTileMap.GetComponent<TilemapRenderer>().sortingOrder = -1;
        this.RegisterEvent<MapGeneratedEvent>(OnMapGenerated)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    void LateUpdate()
    {
        this.GetUtility<ICameraUtility>().LateTick();
    }

    void OnMapGenerated(MapGeneratedEvent e)
    {
        BuildFromModel();
        SetupCamera();
    }

    void BuildFromModel()
    {
        var map = this.GetModel<IMapModel>();
        _floorTileMap.ClearAllTiles();
        _wallTileMap.ClearAllTiles();

        int[,] grid = map.TileGrid;
        if (grid == null) return;

        int w = map.Width;
        int h = map.Height;

        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                var pos = new Vector3Int(i, j, 0);
                switch (grid[i, j])
                {
                    case 0:
                        _wallTileMap.SetTile(pos, _wallTile);
                        break;
                    case 1:
                        _floorTileMap.SetTile(pos, GetEdgeTile(grid, i, j));
                        break;
                    case 2:
                        _wallTileMap.SetTile(pos, _wallTile);
                        break;
                }
            }
        }
    }

    TileBase GetEdgeTile(int[,] grid, int x, int y)
    {
        int w = grid.GetLength(0);
        int h = grid.GetLength(1);

        int mask = 0;
        for (int d = 0; d < 4; d++)
        {
            int nx = x + Dirs[d].x;
            int ny = y + Dirs[d].y;
            if (nx < 0 || nx >= w || ny < 0 || ny >= h || grid[nx, ny] == 2)
                mask |= (1 << d);
        }

        return mask switch
        {
            0 => _floorTile,

            // 四边
            1 => _edgeTop,
            2 => _edgeBottom,
            4 => _edgeLeft,
            8 => _edgeRight,

            // 四角
            5 => _cornerTL,
            9 => _cornerTR,
            6 => _cornerBL,
            10 => _cornerBR,

            // 复杂情况回退
            3 => _edgeTop,
            12 => _edgeLeft,
            7 => _edgeRight,
            11 => _edgeBottom,
            13 => _edgeLeft,
            14 => _edgeTop,
            15 => _floorTile,

            _ => _floorTile
        };
    }

    void SetupCamera()
    {
        var map = this.GetModel<IMapModel>();
        var player = GameObject.FindWithTag("Player");
        if (player != null)
            this.GetUtility<ICameraUtility>().Follow(player.transform);
        this.GetUtility<ICameraUtility>().SetBounds(0, map.Width, 0, map.Height);
    }
}
