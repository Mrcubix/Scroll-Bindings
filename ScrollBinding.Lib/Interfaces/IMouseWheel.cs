namespace ScrollBinding.Lib.Interfaces;

public interface IMouseWheel
{
    public void ScrollForward(int amount);
    
    public void ScrollBackward(int amount);

    public void ScrollLeft(int amount);

    public void ScrollRight(int amount);
}
