using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class InstalledItem : INotifyPropertyChanged
{
	private ulong id;

	private string path;

	private ItemDetails itemDetails;

	public ulong Id
	{
		get
		{
			return id;
		}
		set
		{
			if (id != value)
			{
				id = value;
				NotifyPropertyChanged("Id");
			}
		}
	}

	public string Path
	{
		get
		{
			return path;
		}
		set
		{
			if (!(path == value))
			{
				path = value;
				NotifyPropertyChanged("Path");
			}
		}
	}

	public ItemDetails ItemDetails
	{
		get
		{
			return itemDetails;
		}
		set
		{
			if (itemDetails != value)
			{
				itemDetails = value;
				NotifyPropertyChanged("ItemDetails");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public InstalledItem(ulong id, string path)
	{
		this.id = id;
		this.path = path;
		MonoBehaviourSingleton<EventManager>.Instance.AddEventListener("Event_Client_OnItemDetails", Event_Client_OnItemDetails);
	}

	public void Dispose()
	{
		MonoBehaviourSingleton<EventManager>.Instance.RemoveEventListener("Event_Client_OnItemDetails", Event_Client_OnItemDetails);
	}

	private void Event_Client_OnItemDetails(Dictionary<string, object> message)
	{
		ulong num = (ulong)message["id"];
		string title = (string)message["title"];
		string description = (string)message["description"];
		string previewUrl = (string)message["previewUrl"];
		if (id == num)
		{
			ItemDetails = new ItemDetails
			{
				Title = title,
				Description = description,
				PreviewUrl = previewUrl
			};
		}
	}

	private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
