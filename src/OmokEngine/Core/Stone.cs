namespace GomokuEngine.Core
{
    public enum Stone
    {
        Empty = 0,
        Black = 1,
        White = 2
    }

    public enum MoveType
    {
        Winning,
        DefendFour,
        MakeFour,
        DefendThree,
        MakeThree,
        DefendTwo,
        MakeTwo,
        Strategic,
        Neutral
    }
}
