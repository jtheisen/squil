using System.ComponentModel;

namespace Squil
{
    public class ColumnValueBox : INotifyPropertyChanged
    {
		private String value;

		public String Value
		{
			get { return value; }
			set
			{
				this.value = value;
				PropertyChanged?.Invoke(this, eventArgs);
			}
		}

		static PropertyChangedEventArgs eventArgs = new PropertyChangedEventArgs(nameof(Value));

        public event PropertyChangedEventHandler PropertyChanged;
	}
}
