using System.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using UnityEngine;
using System.Collections;
using System;

[SuppressMessage("ReSharper", "UseStringInterpolation")]
[SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery")]
[SuppressMessage("ReSharper", "MergeConditionalExpression")]
[SuppressMessage("ReSharper", "UseNullPropagation")]
public class CombinationLockModule : MonoBehaviour
{
	private enum Direction
	{
		Left,
		Right
	}

	private const int Max = 20;
	private const float DialIncrement = 360/20f;

	public KMSelectable LeftButton;
	public KMSelectable RightButton;
	public KMSelectable ResetButton;
	public TextMesh DialText;
	public GameObject Dial;
	public AudioClip DialClick;
	public AudioClip DialReset;
	public AudioClip Unlock;

	private KMBombModule BombModule;
	private KMBombInfo BombInfo;
	private KMAudio Audio;

	private IList<int> _inputCode;
	private IList<int> _code;
	private int _currentInput;
	private Direction _currentDirection;
	private bool _isActive;

	void Start()
	{
		Init();
	}

	private void Activate()
	{
		_isActive = true;
		GeneratePassCode();
		BombModule.LogFormat("Initial solution: {0} {1} {2}", _code[0], _code[1], _code[2]);

		StartCoroutine(LogSolution());
	}

	private void Init()
	{
		BombModule = GetComponent<KMBombModule>();
		BombModule.GenerateLogFriendlyName();

		BombInfo = GetComponent<KMBombInfo>();

		Audio = GetComponent<KMAudio>();

		_currentInput = 0;
		_currentDirection = Direction.Right;
		_inputCode = new List<int>(3);
		_code = new List<int>(3);
		SetupButtons();
		BombModule.OnActivate += Activate;
	}

	void Update()
	{
		// ReSharper disable once InvertIf
		if (_isActive && Solved())
		{
			_isActive = false;
			Audio.HandlePlaySoundAtTransform(Unlock.name, transform);
			BombModule.HandlePass();

			BombModule.Log("Module solved");
		}
	}

	private bool Solved()
	{
		if (_inputCode.Count < 3) return false;

		var solved = true;
		GeneratePassCode();

		for (var i = 0; i < _inputCode.Count; i++)
		{
			if (_inputCode[i] == _code[i]) continue;
			solved = false;
			break;
		}

		if (!solved) _inputCode.RemoveAt(2);

		return solved;
	}

	private void SetupButtons()
	{
		LeftButton.OnInteract += delegate
		{
			if (_currentDirection != Direction.Left)
			{
				_inputCode.Add(_currentInput);
				BombModule.LogFormat("Turned left to: {0}", _currentInput);
			}

			Dial.transform.Rotate(0f, DialIncrement, 0f);
			_currentInput--;

			if (_currentInput < 0)
				_currentInput = Max - 1;

			_currentDirection = Direction.Left;
			DialText.text = _currentInput.ToString();

			Audio.HandlePlaySoundAtTransform(DialClick.name, transform);

			return false;
		};

		RightButton.OnInteract += delegate
		{
			if (_currentDirection != Direction.Right)
			{
				_inputCode.Add(_currentInput);
				BombModule.LogFormat("Turned right to: {0}", _currentInput);
			}

			Dial.transform.Rotate(0f, -DialIncrement, 0f);
			_currentInput++;

			if (_currentInput == Max)
				_currentInput = 0;

			if (_inputCode.Count == 2)
			{
				_inputCode.Add(_currentInput);
			}

			_currentDirection = Direction.Right;
			DialText.text = _currentInput.ToString();

			Audio.HandlePlaySoundAtTransform(DialClick.name, transform);

			return false;
		};

		ResetButton.OnInteract += delegate
		{
			BombModule.Log("Pressed reset");

			Dial.transform.Rotate(0f, DialIncrement * _currentInput, 0f);

			_currentInput = 0;
			_currentDirection = Direction.Right;
			_inputCode = new List<int>(3);
			DialText.text = _currentInput.ToString();

			Audio.HandlePlaySoundAtTransform(DialReset.name, transform);

			return false;
		};
	}

	private void GeneratePassCode()
	{
		_code.Clear();
		var twoFactor = BombInfo.GetTwoFactorCodes();
		var numberOfSolvedModules = BombInfo.GetSolvedModuleNames().Count;

		var code1 = 0;
		var code2 = 0;

		if (twoFactor != null && twoFactor.Count() > 0)
		{
			foreach (var twoFactorCode in twoFactor)
			{
				var twoFactorString = twoFactorCode.ToString();
				code1 += int.Parse(twoFactorString[twoFactorString.Length - 1].ToString());
				code2 += int.Parse(twoFactorString[0].ToString());
			}
		}
		else
		{
			code1 = BombInfo.GetSerialNumberNumbers().Last() + numberOfSolvedModules;
			code2 = BombInfo.GetModuleNames().Count;
		}

		code1 += BombInfo.GetBatteryCount();
		code2 += numberOfSolvedModules;

		code1 = code1 >= Max ? code1 % Max : code1;
		code2 = code2 >= Max ? code2 % Max : code2;

		var code3 = code1 + code2;
		code3 = code3 >= Max ? code3 % Max : code3;

		_code.Add(code1);
		_code.Add(code2);
		_code.Add(code3);
	}

	private IEnumerator LogSolution()
	{
		int solved = BombInfo.GetSolvedModuleNames().Count;
		IEnumerable<int> twoFactorCodes = BombInfo.GetTwoFactorCodes();

		do
		{
			yield return new WaitForSeconds(0.1f);
			int newSolved = BombInfo.GetSolvedModuleNames().Count;
			IEnumerable<int> newTwoFactorCodes = BombInfo.GetTwoFactorCodes();
			if ((!twoFactorCodes.SequenceEqual(newTwoFactorCodes) || solved != newSolved) && _isActive)
			{
				GeneratePassCode();
				
				if (solved != newSolved)
				{
					BombModule.LogFormat("Number of solved modules: {0}", newSolved);
				}

				if (!twoFactorCodes.SequenceEqual(newTwoFactorCodes))
				{
					foreach (int twoFactorCode in newTwoFactorCodes)
					{
						BombModule.LogFormat("Two Factor code changed: {0}", twoFactorCode);
					}
				}

				BombModule.LogFormat("New solution: {0} {1} {2}", _code[0], _code[1], _code[2]);

				solved = newSolved;
				twoFactorCodes = newTwoFactorCodes;
			}
		} while (_isActive);
	}

	private int? TryParse(string input)
	{
		int i;
		return int.TryParse(input, out i) ? (int?) i : null;
	}

	public IEnumerator ProcessTwitchCommand(string command)
	{
		string[] split = command.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

		if (split.Length == 4 && split[0] == "submit")
		{
			if (_inputCode.Count > 0)
			{
				ResetButton.OnInteract();
				yield return new WaitForSeconds(1f);
			}

			int?[] numbers = split.Where((_, i) => i > 0).Select(num => TryParse(num)).ToArray();
			if (numbers.All(num => num != null && num > -1 && num < 20))
			{ 
				bool turnDirection = false; // true for left, false for right 
				foreach (int num in numbers)
				{
					KMSelectable button = turnDirection ? LeftButton : RightButton;
					button.OnInteract();
						yield return new WaitForSeconds(0.1f);

					while (_currentInput != num)
					{
						button.OnInteract();
						yield return new WaitForSeconds(0.1f);
					}

					yield return new WaitForSeconds(0.3f);

					turnDirection = !turnDirection;
				}
			}
		}
	}
}
