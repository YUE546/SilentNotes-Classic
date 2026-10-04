using System;
using System.Collections.Generic;
using System.Linq;
using SilentNotes.Models;

namespace SilentNotes.Workers;

public class NoteRepositoryMerger
{
	public NoteRepositoryModel Merge(NoteRepositoryModel localRepository, NoteRepositoryModel remoteRepository)
	{
		if (localRepository == null)
		{
			throw new ArgumentNullException("localRepository");
		}
		if (remoteRepository == null)
		{
			throw new ArgumentNullException("remoteRepository");
		}
		DeletedNoteListModel deletedNotes = BuildMergedListOfDeletedNotes(localRepository, remoteRepository);
		NoteListModel noteListModel = BuildListOfLivingNotes(localRepository, deletedNotes);
		NoteListModel noteListModel2 = BuildListOfLivingNotes(remoteRepository, deletedNotes);
		NoteRepositoryModel noteRepositoryModel = new NoteRepositoryModel();
		noteRepositoryModel.Revision = 9;
		noteRepositoryModel.Id = remoteRepository.Id;
		noteRepositoryModel.DeletedNotes = deletedNotes;
		noteRepositoryModel.Safes = BuildMergedListOfSafes(remoteRepository.Safes, localRepository.Safes);
		if (localRepository.OrderModifiedAt > remoteRepository.OrderModifiedAt)
		{
			noteRepositoryModel.Notes = BuildMergedListOfNotes(noteListModel, noteListModel2);
			noteRepositoryModel.OrderModifiedAt = localRepository.OrderModifiedAt;
		}
		else
		{
			noteRepositoryModel.Notes = BuildMergedListOfNotes(noteListModel2, noteListModel);
			noteRepositoryModel.OrderModifiedAt = remoteRepository.OrderModifiedAt;
		}
		noteRepositoryModel.RemoveUnusedSafes();
		BringPinnedToTop(noteRepositoryModel.Notes);
		return noteRepositoryModel;
	}

	private DeletedNoteListModel BuildMergedListOfDeletedNotes(NoteRepositoryModel localRepository, NoteRepositoryModel remoteRepository)
	{
		DeletedNoteListModel deletedNoteListModel = new DeletedNoteListModel();
		deletedNoteListModel.AddRange(remoteRepository.DeletedNotes.Select((DeletedNoteModel item) => item.Clone()));
		foreach (DeletedNoteModel deletedNote in localRepository.DeletedNotes)
		{
			DeletedNoteModel deletedNoteModel = deletedNoteListModel.FindById(deletedNote.Id);
			if (deletedNoteModel != null)
			{
				if (deletedNote.DeletedAt > deletedNoteModel.DeletedAt)
				{
					deletedNoteModel.DeletedAt = deletedNote.DeletedAt;
				}
			}
			else if (remoteRepository.Notes.ContainsById(deletedNote.Id))
			{
				deletedNoteListModel.Add(deletedNote.Clone());
			}
		}
		deletedNoteListModel.Sort(new DeletedNoteModelIdComparer());
		return deletedNoteListModel;
	}

	private NoteListModel BuildListOfLivingNotes(NoteRepositoryModel repository, List<DeletedNoteModel> deletedNotes)
	{
		NoteListModel noteListModel = new NoteListModel();
		DeletedNoteModelIdComparer comparer = new DeletedNoteModelIdComparer();
		DeletedNoteModel deletedNoteModel = new DeletedNoteModel();
		foreach (NoteModel note in repository.Notes)
		{
			deletedNoteModel.Id = note.Id;
			int num = deletedNotes.BinarySearch(deletedNoteModel, comparer);
			if (num < 0 || note.CreatedAt > deletedNotes[num].DeletedAt)
			{
				noteListModel.Add(note);
			}
		}
		return noteListModel;
	}

	private static NoteListModel BuildMergedListOfNotes(NoteListModel leftItems, NoteListModel rightItems)
	{
		NoteListModel noteListModel = new NoteListModel();
		List<Tuple<NoteModel, NoteModel>> list = OuterJoin(leftItems, rightItems, (NoteModel item) => item.Id);
		foreach (Tuple<NoteModel, NoteModel> item in list)
		{
			if (item.Item1 == null)
			{
				noteListModel.Add(item.Item2.Clone());
				continue;
			}
			if (item.Item2 == null)
			{
				noteListModel.Add(item.Item1.Clone());
				continue;
			}
			NoteModel noteModel = ChooseLastModified(item.Item1, item.Item2, (NoteModel item) => item.ModifiedAt, (NoteModel item) => item.MetaModifiedAt);
			noteListModel.Add(noteModel.Clone());
		}
		return noteListModel;
	}

	private static SafeListModel BuildMergedListOfSafes(SafeListModel leftItems, SafeListModel rightItems)
	{
		SafeListModel safeListModel = new SafeListModel();
		List<Tuple<SafeModel, SafeModel>> list = OuterJoin(leftItems, rightItems, (SafeModel item) => item.Id);
		foreach (Tuple<SafeModel, SafeModel> item in list)
		{
			if (item.Item1 == null)
			{
				safeListModel.Add(item.Item2.Clone());
				continue;
			}
			if (item.Item2 == null)
			{
				safeListModel.Add(item.Item1.Clone());
				continue;
			}
			SafeModel safeModel = ChooseLastModified(item.Item1, item.Item2, (SafeModel item) => item.ModifiedAt, null);
			safeListModel.Add(safeModel.Clone());
		}
		return safeListModel;
	}

	private static List<Tuple<TItem, TItem>> OuterJoin<TItem, TKey>(IList<TItem> leftItems, IList<TItem> rightItems, Func<TItem, TKey> keySelector) where TItem : class
	{
		List<Tuple<TItem, TItem>> list = new List<Tuple<TItem, TItem>>();
		Dictionary<TKey, int> dictionary = new Dictionary<TKey, int>();
		Dictionary<TKey, int> dictionary2 = new Dictionary<TKey, int>();
		for (int i = 0; i < leftItems.Count; i++)
		{
			dictionary.Add(keySelector(leftItems[i]), i);
		}
		for (int j = 0; j < rightItems.Count; j++)
		{
			dictionary2.Add(keySelector(rightItems[j]), j);
		}
		int pos = 0;
		int pos2 = 0;
		AddAdjacentSingles(list, leftItems, ref pos, fromLeftSide: true, keySelector, dictionary2);
		AddAdjacentSingles(list, rightItems, ref pos2, fromLeftSide: false, keySelector, dictionary);
		while (pos < leftItems.Count)
		{
			TItem val = leftItems[pos];
			pos2 = dictionary2[keySelector(val)];
			list.Add(new Tuple<TItem, TItem>(val, rightItems[pos2]));
			pos++;
			pos2++;
			AddAdjacentSingles(list, leftItems, ref pos, fromLeftSide: true, keySelector, dictionary2);
			AddAdjacentSingles(list, rightItems, ref pos2, fromLeftSide: false, keySelector, dictionary);
		}
		return list;
	}

	private static void AddAdjacentSingles<TItem, TKey>(List<Tuple<TItem, TItem>> map, IList<TItem> candidates, ref int pos, bool fromLeftSide, Func<TItem, TKey> keySelector, Dictionary<TKey, int> partnerKeys) where TItem : class
	{
		bool flag = true;
		while (flag && pos < candidates.Count)
		{
			TItem val = candidates[pos];
			flag = !partnerKeys.ContainsKey(keySelector(val));
			if (flag)
			{
				if (fromLeftSide)
				{
					map.Add(new Tuple<TItem, TItem>(val, null));
				}
				else
				{
					map.Add(new Tuple<TItem, TItem>(null, val));
				}
				pos++;
			}
		}
	}

	internal static TItem ChooseLastModified<TItem>(TItem item1, TItem item2, Func<TItem, DateTime> modifiedAtSelector, Func<TItem, DateTime?> metaModifiedAtSelector)
	{
		int num = DateTime.Compare(modifiedAtSelector(item1), modifiedAtSelector(item2));
		if (num == 0 && metaModifiedAtSelector != null)
		{
			num = Nullable.Compare(metaModifiedAtSelector(item1), metaModifiedAtSelector(item2));
		}
		return (num >= 0) ? item1 : item2;
	}

	private static void BringPinnedToTop(NoteListModel notes)
	{
		int num = notes.IndexOfFirstUnpinnedNote();
		if (num < 0)
		{
			return;
		}
		for (int i = num; i < notes.Count; i++)
		{
			NoteModel noteModel = notes[i];
			if (noteModel.IsPinned)
			{
				notes.RemoveAt(i);
				notes.Insert(0, noteModel);
			}
		}
	}
}
