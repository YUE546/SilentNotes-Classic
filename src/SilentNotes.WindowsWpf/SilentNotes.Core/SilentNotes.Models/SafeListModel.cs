using System;
using System.Collections.Generic;
using System.Linq;
using SilentNotes.Services;

namespace SilentNotes.Models;

public class SafeListModel : List<SafeModel>
{
	public SafeListModel()
	{
	}

	public SafeListModel(IEnumerable<SafeModel> collection)
		: base(collection)
	{
	}

	public SafeModel FindById(Guid? id)
	{
		if (!id.HasValue)
		{
			return null;
		}
		return Find(delegate(SafeModel item)
		{
			Guid id2 = item.Id;
			Guid? guid = id;
			return id2 == guid;
		});
	}

	public SafeModel FindOldestOpenSafe(ISafeKeyService keyService)
	{
		SafeModel safeModel = null;
		foreach (SafeModel item in this.Where((SafeModel item) => keyService.IsSafeOpen(item.Id)))
		{
			if (safeModel == null)
			{
				safeModel = item;
			}
			else if (item.CreatedAt < safeModel.CreatedAt)
			{
				safeModel = item;
			}
		}
		return safeModel;
	}
}
