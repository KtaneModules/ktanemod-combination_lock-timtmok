using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Random = UnityEngine.Random;

public class TwoFactorWidget : MonoBehaviour
{
	public TextMesh KeyText;
	public AudioClip Notify;

	private int _key;
	private float _timeElapsed;

	private const float TimerLength = 60.0f;

	public static string WidgetQueryTwofactor = "twofactor";
	public static string WidgetTwofactorKey = "twofactor_key";

	void Awake ()
	{
		GetComponent<KMWidget>().OnQueryRequest += GetQueryResponse;
		GetComponent<KMWidget>().OnWidgetActivate += Activate;
		GenerateKey();
	}

	void Update()
	{
		_timeElapsed += Time.deltaTime;

		// ReSharper disable once InvertIf
		if (_timeElapsed >= TimerLength)
		{
			_timeElapsed = 0f;
			UpdateKey();
		}
	}

	private void Activate()
	{
		Debug.Log("[TwoFactorWidget] Two Factor present");

		_timeElapsed = 0f;
		DisplayKey();
	}

	void UpdateKey()
	{
		GetComponent<KMAudio>().HandlePlaySoundAtTransform(Notify.name, transform);
		GenerateKey();
		DisplayKey();
	}

	private void GenerateKey()
	{
		_key = Random.Range(0, 1000000);
	}

	private void DisplayKey()
	{
		KeyText.text = _key.ToString() + ".";
	}

	private string GetQueryResponse(string querykey, string queryinfo)
	{
		if (querykey != WidgetQueryTwofactor) return string.Empty;

		var response = new Dictionary<string, int> {{WidgetTwofactorKey, _key}};
		var serializedResponse = JsonConvert.SerializeObject(response);

		return serializedResponse;
	}
}
