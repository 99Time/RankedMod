using System;
using System.Diagnostics;
using System.Threading;

namespace Codebase;

public class PausableTimer : IDisposable
{
	private readonly Timer _timer;

	private readonly Stopwatch _stopwatch = new Stopwatch();

	private readonly Action _callback;

	private bool _callbackCalled;

	private readonly long _intervalMilliseconds;

	private bool _isRunning;

	public long MillisecondsLeft => _intervalMilliseconds - _stopwatch.ElapsedMilliseconds;

	public PausableTimer(Action callback, long intervalMilliseconds)
	{
		_callback = callback ?? throw new ArgumentNullException("callback");
		_intervalMilliseconds = intervalMilliseconds;
		_timer = new Timer(TimerCallback, null, -1, -1);
	}

	public void Start()
	{
		if (!_isRunning)
		{
			_stopwatch.Start();
			_timer.Change(_intervalMilliseconds - _stopwatch.ElapsedMilliseconds, -1L);
			_isRunning = true;
		}
	}

	public void Pause()
	{
		if (_isRunning)
		{
			_stopwatch.Stop();
			_timer.Change(-1, -1);
			_isRunning = false;
		}
	}

	public void Reset()
	{
		_stopwatch.Reset();
		Pause();
	}

	public void TimerCallback(object state)
	{
		_callbackCalled = true;
		_callback();
	}

	public bool TimerEnded()
	{
		return _callbackCalled;
	}

	public void Dispose()
	{
		_timer?.Dispose();
		_stopwatch?.Stop();
	}
}
