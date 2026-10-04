using System;

namespace SilentNotes.Workers;

public class TimeAgo
{
	public class Localization
	{
		public string Today { get; set; }

		public string Yesterday { get; set; }

		public string NumberOfDaysAgo { get; set; }

		public string NumberOfWeeksAgo { get; set; }

		public string NumberOfMonthsAgo { get; set; }

		public string NumberOfYearsAgo { get; set; }

		public Localization()
		{
			Today = "today";
			Yesterday = "yesterday";
			NumberOfDaysAgo = "{0} days ago";
			NumberOfWeeksAgo = "{0} weeks ago";
			NumberOfMonthsAgo = "{0} month ago";
			NumberOfYearsAgo = "{0} years ago";
		}
	}

	private readonly Localization _localization;

	public TimeAgo(Localization localization)
	{
		_localization = localization;
	}

	public string PrettyPrint(DateTime formerEvent, DateTime laterEvent)
	{
		DateTime date = formerEvent.Date;
		DateTime date2 = laterEvent.Date;
		if (date > date2)
		{
			return null;
		}
		double totalDays = date2.Subtract(date).TotalDays;
		if (totalDays < 1.0)
		{
			return _localization.Today;
		}
		if (totalDays < 2.0)
		{
			return _localization.Yesterday;
		}
		if (totalDays < 14.0)
		{
			return string.Format(_localization.NumberOfDaysAgo, totalDays);
		}
		int num = AgeInMonths(date, date2);
		if (num < 2)
		{
			return string.Format(_localization.NumberOfWeeksAgo, AgeInWeeks(totalDays));
		}
		int num2 = AgeInYears(date, date2);
		if (num2 < 2)
		{
			return string.Format(_localization.NumberOfMonthsAgo, num);
		}
		return string.Format(_localization.NumberOfYearsAgo, num2);
	}

	public int AgeInYears(DateTime birthday, DateTime ageAt)
	{
		if (ageAt < birthday)
		{
			return 0;
		}
		int num = ageAt.Year - birthday.Year;
		if (ageAt.Month < birthday.Month || (ageAt.Month == birthday.Month && ageAt.Day < birthday.Day))
		{
			num--;
		}
		return num;
	}

	public int AgeInMonths(DateTime birthday, DateTime ageAt)
	{
		if (ageAt < birthday)
		{
			return 0;
		}
		int num = 12 * ageAt.Year + ageAt.Month - (12 * birthday.Year + birthday.Month);
		if (ageAt.Day < birthday.Day)
		{
			num--;
		}
		return num;
	}

	internal int AgeInWeeks(double numberOfDays)
	{
		return (int)numberOfDays / 7;
	}
}
