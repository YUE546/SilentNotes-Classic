namespace SilentNotes.Models;

public class WallpaperModel
{
	public string Id { get; private set; }

	public string Image { get; private set; }

	public WallpaperModel(string id, string image)
	{
		Id = id;
		Image = image;
	}
}
