# Reloadify3000
[![NuGet](https://img.shields.io/nuget/v/Reloadify3000.svg)](https://www.nuget.org/packages/Reloadify3000)

What am I? I am a nuget and IDE Extension to help with developing HotReload libraries.


# Whats included?

* ESP
	* A nice little library that deals with establishing communication between the IDE and the code base.
* IDE Extensions
* In App Roslyn compilation
* Notifcation for when a new DataType is available


# How it works?

Reloadify listents to text change notifications, and Document saves in your IDE.

When this happens, we collect the changes and send them to the client lib.

The client lib then takes those changes and compiles a new DLL, with new Types.

Reloadify then sends a notification of all the types that changed, so your library can reload those types.


# How do I use it?

Install the [Extension](https://github.com/Clancey/Reloadify3000/releases)

Install the [nuget](https://www.nuget.org/packages/Reloadify3000).

Now just hook into the reload notification

``` cs 
Reloadify.Reload.Instance.ReplaceType = (d) => {
	string oldType = d.ClassName;
	Type newType = d.Type;
};

Reloadify.Reload.Instance.FinishedReload = () =>{
	//All new types are in! Lets reload!
};

```
And now initialize it!

``` cs
Reloadify.Reload.Init();
```


# Disclaimer
Reloadify is a proof of concept. There is no official support. Use at your own Risk.


# Thanks
Thank you Frank for the original [proof of concept](https://github.com/praeclarum/Continuous)!

	 