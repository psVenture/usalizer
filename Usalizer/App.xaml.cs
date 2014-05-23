using System;
using System.Windows;
using System.Data;
using System.Windows.Threading;
using System.Xml;
using System.Configuration;

namespace Usalizer
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			MessageBox.Show(e.Exception.ToString());
			e.Handled = true;
			Environment.Exit(0);
		}
	}
}