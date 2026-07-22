//¼Ü¹¹Èë¿Ú
public class RogueLikeGame : Architecture<RogueLikeGame>
{
    protected override void Init()
    {
        this.RegisterModel<IPlayerModel>(new PlayerModel());
    }
}