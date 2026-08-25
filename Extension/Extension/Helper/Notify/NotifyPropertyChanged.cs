using System.ComponentModel;

namespace Extension.Helper.Notify
{
    public class NotifyPropertyChanged : INotifyPropertyChanged
    {
    	public event PropertyChangedEventHandler PropertyChanged;

    	protected virtual void OnPropertyChanged(string propertyName )
    	{
    		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    	}
    }
}
