using System.Xml.Linq;

namespace SilentNotes.Workers;

public interface INoteRepositoryUpdater
{
	bool IsTooNewForThisApp(XDocument repository);

	bool Update(XDocument repository);
}
