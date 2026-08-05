using PixelChroma;
using UnityEngine;

public class DeathSnapshot
{
    public Vector3 Position { get; private set; }
    public Quaternion Rotation { get; private set; }
    public PlayerPaintCanvas.PaintFormType FormType { get; private set; }
    public Color32[] MaskPixels { get; private set; }
    public bool HasSnapshot { get; private set; }
    public int FrameIndex { get; private set; }
    public bool FlipX { get; private set; }
    public void Capture(Transform transform, PlayerPaintCanvas paintCanvas)
    {
        Position = transform.position;
        Rotation = transform.rotation;

        if (paintCanvas != null)
        {
            FormType = paintCanvas._activeFormType;
            FrameIndex = paintCanvas.GetCurrentFrameIndex();
            FlipX = paintCanvas.CurrentFlipX;
        }
    }

    public void RestorePosition(Transform transform)
    {
        transform.position = Position; 
        transform.rotation = Quaternion.identity;
    }

    public void Clear()
    {
        MaskPixels = null;
        HasSnapshot = false;
    }
}