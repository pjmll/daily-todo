using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DailyTodo
{
    internal sealed class CatCollection
    {
        public const int DaysPerCat = 3;
        private readonly string path;
        private readonly Random random = new Random();
        private readonly DataContractJsonSerializer serializer = new DataContractJsonSerializer(
            typeof(Progress), new DataContractJsonSerializerSettings
            {
                DateTimeFormat = new DateTimeFormat("yyyy-MM-dd", CultureInfo.InvariantCulture)
            });
        private Progress saved;
        private bool readError;

        public int Streak { get; private set; }
        public int DaysUntilReward { get; private set; }
        public string NewCats { get; private set; }
        public bool HasStorageError { get; private set; }
        public IReadOnlyList<DeskPet.Skin> Catalog { get; private set; }
        public int UnlockedCount { get { return Catalog.Count(IsUnlocked); } }
        public DeskPet.Skin Selected { get { return Catalog.FirstOrDefault(skin => skin.Id == saved.Selected) ?? Catalog[0]; } }

        [DataContract]
        private sealed class Progress
        {
            [DataMember(IsRequired = true)] public List<string> Unlocked { get; set; }
            [DataMember(IsRequired = true)] public string Selected { get; set; }
            [DataMember(EmitDefaultValue = false)] public DateTime? RewardedThrough { get; set; }
        }

        public CatCollection(string path)
        {
            this.path = path;
            Catalog = DeskPet.LoadCatalog(Path.GetDirectoryName(path));
            var starter = Catalog[0].Id;
            saved = new Progress { Unlocked = new List<string> { starter }, Selected = starter };
            try
            {
                using (var file = File.OpenRead(path))
                {
                    var loaded = (Progress)serializer.ReadObject(file);
                    if (loaded == null || loaded.Unlocked == null || !loaded.Unlocked.Contains(starter) ||
                        !loaded.Unlocked.Contains(loaded.Selected) ||
                        loaded.Unlocked.Distinct().Count() != loaded.Unlocked.Count ||
                        loaded.Unlocked.Any(string.IsNullOrWhiteSpace))
                        throw new InvalidDataException("Invalid cat collection.");
                    saved = loaded;
                }
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (Exception error)
            {
                if (!TaskStore.IsStorageError(error)) throw;
                readError = true;
                HasStorageError = true;
            }
        }

        public bool IsUnlocked(DeskPet.Skin skin)
        {
            return saved.Unlocked.Contains(skin.Id);
        }

        public bool Select(DeskPet.Skin skin)
        {
            if (!IsUnlocked(skin)) return false;
            if (saved.Selected == skin.Id) return true;
            return Save(new Progress
            {
                Selected = skin.Id, Unlocked = saved.Unlocked, RewardedThrough = saved.RewardedThrough
            });
        }

        public void AcknowledgeNewCats()
        {
            NewCats = null;
        }

        public void Refresh(IEnumerable<TodoTask> tasks, DateTime today)
        {
            var days = tasks.Where(task => task.Completed.HasValue)
                .Select(task => task.Completed.Value.Date).Where(day => day <= today)
                .Distinct().OrderBy(day => day).ToArray();

            var locked = Catalog.Where(skin => !IsUnlocked(skin)).ToList();
            var newlyUnlocked = new List<DeskPet.Skin>();
            var through = saved.RewardedThrough;
            DateTime? previous = null;
            int run = 0;
            foreach (var day in days)
            {
                if (through.HasValue && day <= through.Value) continue;
                if (locked.Count == 0) break;
                run = previous.HasValue && (day - previous.Value).Days == 1 ? run + 1 : 1;
                previous = day;
                if (run != DaysPerCat) continue;

                int index = random.Next(locked.Count);
                newlyUnlocked.Add(locked[index]);
                locked.RemoveAt(index);
                through = day;
                run = 0;
            }

            if (newlyUnlocked.Count > 0 && Save(new Progress
            {
                Selected = saved.Selected,
                Unlocked = saved.Unlocked.Concat(newlyUnlocked.Select(skin => skin.Id)).ToList(),
                RewardedThrough = through
            }))
            {
                var names = string.Join("、", newlyUnlocked.Select(skin => skin.Name));
                NewCats = NewCats == null ? names : NewCats + "、" + names;
            }

            Streak = 0;
            int last = days.Length - 1;
            if (last >= 0 && (today - days[last]).Days <= 1)
            {
                Streak = 1;
                for (int index = last; index > 0 && (days[index] - days[index - 1]).Days == 1; index--)
                    Streak++;
            }

            int eligible = Streak;
            if (Streak > 0 && saved.RewardedThrough.HasValue)
                eligible = Math.Min(Streak, Math.Max(0, (days[last] - saved.RewardedThrough.Value).Days));
            DaysUntilReward = UnlockedCount == Catalog.Count ? 0 : Math.Max(0, DaysPerCat - eligible);
        }

        private bool Save(Progress progress)
        {
            if (readError) return false;
            try
            {
                TaskStore.SaveAtomically(path, serializer, progress);
                saved = progress;
                HasStorageError = false;
                return true;
            }
            catch (Exception error)
            {
                if (!TaskStore.IsStorageError(error)) throw;
                HasStorageError = true;
                return false;
            }
        }
    }
}
