using System;
using System.IO;

namespace SilentNotes.Workers;

public class AtomicFileWriter
{
	public class TestSimulation
	{
		public bool SimulateWriteError { get; set; }

		public bool SimulateReplaceError { get; set; }
	}

	private readonly TestSimulation _testSimulation;

	public long MinValidFileSize { get; set; }

	public AtomicFileWriter()
		: this(null)
	{
	}

	public AtomicFileWriter(TestSimulation testParameters)
	{
		_testSimulation = testParameters;
	}

	public void Write(string filePath, Action<FileStream> writingDelegate)
	{
		if (string.IsNullOrWhiteSpace(filePath))
		{
			throw new ArgumentNullException("filePath");
		}
		if (writingDelegate == null)
		{
			throw new ArgumentNullException("writingDelegate");
		}
		string directoryName = Path.GetDirectoryName(filePath);
		Directory.CreateDirectory(directoryName);
		string readyFilePath = GetReadyFilePath(filePath);
		if (File.Exists(readyFilePath))
		{
			throw new UnfinishedAtomicFileWritingException(filePath);
		}
		string tempFilePath = GetTempFilePath(filePath);
		using (FileStream obj = new FileStream(tempFilePath, FileMode.Create))
		{
			if (_testSimulation != null && _testSimulation.SimulateWriteError)
			{
				throw new Exception("SimulateWriteError");
			}
			writingDelegate(obj);
		}
		File.WriteAllBytes(readyFilePath, new byte[1] { 32 });
		CompletePendingWrite(filePath);
	}

	public void CompletePendingWrite(string filePath)
	{
		string readyFilePath = GetReadyFilePath(filePath);
		if (!File.Exists(readyFilePath))
		{
			return;
		}
		string tempFilePath = GetTempFilePath(filePath);
		if (FileExistsAndHasValidSize(tempFilePath))
		{
			if (_testSimulation != null && _testSimulation.SimulateReplaceError)
			{
				throw new Exception("SimulateReplaceError");
			}
			File.Copy(tempFilePath, filePath, overwrite: true);
			if (!FileExistsAndHasValidSize(filePath))
			{
				throw new UnfinishedAtomicFileWritingException(filePath);
			}
			TryDeleteFile(readyFilePath);
			TryDeleteFile(tempFilePath);
		}
		else
		{
			TryDeleteFile(readyFilePath);
			TryDeleteFile(tempFilePath);
		}
	}

	private static string GetTempFilePath(string filePath)
	{
		return filePath + ".new";
	}

	private static string GetReadyFilePath(string filePath)
	{
		return filePath + ".ready";
	}

	private bool FileExistsAndHasValidSize(string filePath)
	{
		if (!File.Exists(filePath))
		{
			return false;
		}
		if (MinValidFileSize == 0)
		{
			return true;
		}
		long length = new FileInfo(filePath).Length;
		return length >= MinValidFileSize;
	}

	private static bool TryDeleteFile(string filePath)
	{
		try
		{
			File.Delete(filePath);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
