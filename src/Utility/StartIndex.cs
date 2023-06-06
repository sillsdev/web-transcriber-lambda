namespace SIL.Transcriber.Utility;

public static class StartIndex
{
    const int DIVISOR = 10000000;
    public static int GetStart(int start, 
            ref int startId)
    {
        bool hasId = start / DIVISOR != 0;
        startId = hasId ? start % DIVISOR : 0;
        return hasId ? start / DIVISOR : start;
    }

    public static int SetStart(int start, ref int lastId)
    { //lastId == -1, we're done with this start
      //otherwise keep start the same and exit loop
        start = lastId >= 0 ? start * DIVISOR + lastId : start + 1;
        lastId = lastId > 0 ? lastId : 0;
        return start;
    }
}
