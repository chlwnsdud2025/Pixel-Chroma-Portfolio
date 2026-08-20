namespace PixelChroma.UI
{
    public static class PaintToolCursor
    {
        public static void Apply(BrushMode mode, int brushSize)
        {
            if (CursorManager.Instance == null) return;

            float brushScale = 1f + (brushSize * 0.1f);

            switch (mode)
            {
                case BrushMode.Eyedropper:
                    CursorManager.Instance.SetCursorType(CursorType.Eyedropper);
                    break;
                case BrushMode.Fill:
                    CursorManager.Instance.SetCursorType(CursorType.Fill);
                    break;
                case BrushMode.Eraser:
                    CursorManager.Instance.SetCursorType(CursorType.Eraser, brushScale);
                    break;
                case BrushMode.Brush:
                default:
                    CursorManager.Instance.SetCursorType(CursorType.Brush, brushScale);
                    break;
            }
        }
    }
}
