using UnityEngine;
using UnityEngine.Tilemaps;
public class MapBuilder : MonoBehaviour, IController
{
    [SerializeField] private Tilemap _floorTileMap;
    [SerializeField] private Tilemap _wallTileMap;
    [SerializeField] private TileBase _floorTile;
    [SerializeField] private TileBase _wallTile;
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Awake()
    {
        this.RegisterEvent<MapGeneratedEvent>(OnMapGenerated).UnRegisterWhenGameObjectDestroyed(gameObject);
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

    /// <summary>
    /// ����ͼ
    /// </summary>
    void BuildFromModel()
    {
        var map=this.GetModel<IMapModel>();
        _floorTileMap.ClearAllTiles();//��յ�ͼ
        _wallTileMap.ClearAllTiles();
        //�õ����ľ���
        int[,] grid = map.TileGrid;
        if(grid==null)return;

        int w = map.Width;
        int h = map.Height;
        for(int i = 0; i < w; i++)
        {
            for(int j = 0; j < h; j++)
            {
                var pos=new Vector3Int(i,j,0);
                switch (grid[i, j])
                {
                    case 1: _floorTileMap.SetTile(pos, _floorTile); break;
                    case 2: _wallTileMap.SetTile(pos, _wallTile); break;
                }
            }
        }

    }

    /// <summary>
    /// �������
    /// </summary>
    void SetupCamera()
    {
        var map = this.GetModel<IMapModel>();
        var player= GameObject.FindWithTag("Player");
        if (player != null)
        {
            this.GetUtility<ICameraUtility>().Follow(player.transform);
        }
        this.GetUtility<ICameraUtility>().SetBounds(0, map.Width, 0, map.Height);
    }
}