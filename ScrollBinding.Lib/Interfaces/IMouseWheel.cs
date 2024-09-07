namespace ScrollBinding.Lib.Interfaces;

public interface IMouseWheel
{
    public void ScrollVertically(int amount);

    public void ScrollHorizontally(int amount);

    public void Flush();
}
