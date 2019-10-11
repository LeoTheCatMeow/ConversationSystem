using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ConversationSystem : MonoBehaviour {

	[Header("References")]
	public Text title;
	public Text content; 
	public Transform options; 
	public List<Image> sprites;

	[Header("Custom Properties")]
	public string main_character_name;
	public bool typewriter_effect;

	//initialization setup
	private const string conversation_files_folder = "Conversations";
	private const string conversation_sprites_folder = "ConversationSprites";

	//references
	public static ConversationSystem instance = null; 

	//events
	public delegate void key_reached_delegate (string key);
	public static event key_reached_delegate key_reached; 
	public delegate void variable_key_delegate (out string key);
	public static event variable_key_delegate variable_key;

	//collections
	private static Dictionary<string, string> conversation_collection = new Dictionary<string, string> (); //string as key : conversation content
	private static Dictionary<string, Sprite> sprite_collection = new Dictionary<string, Sprite> (); //character sprites to accompany conversation
	private List<string> current_coversation_breakdown = new List<string> (); //content and options
	private List<string> current_main_text_breakdown = new List<string> (); //content, broken down into segments, each including a title and a text 
	private Dictionary<Text, string> option_textfield_collection = new Dictionary<Text, string> (); //textfield as key : string used as key in conversation_collection

	//status
	public static bool active = false;
	private static bool initialized = false; 
	private bool isTyping = false; 
	private string text_to_type = ""; 

	void Awake () {
		if (instance == null) {
			instance = this;
		} else if (instance != this) {
			Destroy (gameObject);
		}
	}

	void OnDestroy () {
		if (instance == this) {
			instance = null;
		}
	}

	public static void Initialize() {
		if (initialized) {
			return;
		}
		TextAsset[] conversation_files = Resources.LoadAll (conversation_files_folder, typeof(TextAsset)).Cast<TextAsset>().ToArray();
		foreach (TextAsset conversation_file in conversation_files) {
			//split the text file by new lines, empty lines will be removed 
			string[] conversations = conversation_file.text.Split (new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);

			//every conversation is divided into key (not shown in game) and body (shown in game), they are then added to the conversation collection
			foreach (string conversation in conversations) {
				if (conversation [0] == '#') {
					continue; 
				}
				string[] conversation_components = conversation.Split ('|');
				string key = conversation_components [0].Trim (); 
				string body = conversation_components [1].Trim ();
				conversation_collection.Add (key, body);
			}
		}
		Sprite[] conversation_sprites = Resources.LoadAll (conversation_sprites_folder, typeof(Sprite)).Cast<Sprite> ().ToArray ();
		foreach (Sprite conversation_sprite in conversation_sprites) {
			sprite_collection.Add (conversation_sprite.name, conversation_sprite);
		}

		initialized = true; 
	}

	void Start () {
		//establish references with buttons on screen 
		Text[] option_texts = options.GetComponentsInChildren<Text> (); 
		foreach (Text t in option_texts) { 
			option_textfield_collection.Add (t, null);
		}
	}
    
	public static void EnterConversation (string key) {
		instance._EnterConversation (key);
	}

	public void _EnterConversation (string key) {
		active = true;
		GoToKey (key);
		//custom animation
		GetComponent<Animator> ().Play ("In");
	}

	public static void ExitConversation () {
		instance._ExitConversation ();
	}

	public void _ExitConversation () {
		active = false; 
		options.gameObject.SetActive (false);
		//custom animation
		GetComponent<Animator> ().Play ("Out");
	}

	//go to the next series of conversation after a text object (an option) is selected 
	public void GoToKey (Text target) {
		GoToKey (option_textfield_collection [target]);
	}

	//go to a series of conversation using a string as a key 
	public void GoToKey (string key) {
		if (key_reached != null) {
			key_reached (key);
		}

		if (key == "") {
			ExitConversation ();
			return;
		}
			
		//experimental 
		if (key [0] == '#') {
			if (variable_key != null) {
				variable_key (out key);
			}
		}

		//first, hide all previously shown options and bring up the content, clear any existing title
		content.gameObject.SetActive (true);
		options.gameObject.SetActive (false);
		title.text = "";

		//find the directory in the dictionary
		string body = conversation_collection [key]; 
		current_coversation_breakdown = body.Split ('(').ToList();

		//break the main text down and display the first part, the subsequent parts will be triggered on mouse-click
		string main_text = current_coversation_breakdown [0].Trim ();
		current_main_text_breakdown = main_text.Split ('`').ToList();
		DisplayConversation ();
	}

	public void DisplayConversation () {
		if (isTyping) {
			StopAllCoroutines ();
			content.text = text_to_type;
			isTyping = false; 
			return; 
		}

		if (current_main_text_breakdown.Count == 0) {
			DisplayOptions ();
			return;
		}

		string text_to_display = current_main_text_breakdown.First().Trim();
		current_main_text_breakdown.RemoveAt (0);

		if (text_to_display.Contains ('[')) {
			string[] text_and_sprites = text_to_display.Split ('[');
			text_to_display = text_and_sprites [0].Trim ();
			string sprite_names = text_and_sprites [1].Trim ();
			sprite_names = sprite_names.TrimEnd (']');
			DisplaySprites (sprite_names);
		}

		if (text_to_display.Contains ('\\')) {
			string[] title_and_text = text_to_display.Split ('\\');
			title.text = title_and_text [0].Trim ();
			text_to_display = title_and_text [1].Trim ();
		} 

		if (typewriter_effect) {
			content.text = "";
			text_to_type = text_to_display;
			StartCoroutine (AppendText (text_to_type));
			return;
		}
		content.text = text_to_display;
	}

	public void DisplaySprites (string sprite_names) {
		string[] names = sprite_names.Split (',');
		for (int i = 0; i < Mathf.Min (names.Length, sprites.Count); i++) {
			string name = names [i].Trim ();
			Sprite s = null;
			if (sprite_collection.ContainsKey (name)) {
				s = sprite_collection [name];
			} 
			StartCoroutine (ChangeSprite (s, sprites[i]));
		}
	}

	public void DisplayOptions () {
		//check if there are options
		if (current_coversation_breakdown.Count <= 1) {
			ExitConversation ();
			return;
		}

		//check if there's only 1 option
		if (current_coversation_breakdown.Count == 2) {
			string[] options = current_coversation_breakdown [1].Split (')');
			if (options[1].Trim() == "") {
				GoToKey (options[0]);
				return;
			}
		}
			
		//display the options, hide the content area and the title
		content.gameObject.SetActive (false);
		options.gameObject.SetActive (true);
		title.text = main_character_name;

		Text[] option_textfields = option_textfield_collection.Keys.ToArray();
		for (int i = 1; i < current_coversation_breakdown.Count; i++) {
			string[] option_components = current_coversation_breakdown [i].Split (')');

			//store the key
			string key = option_components [0].Trim();
			option_textfield_collection[option_textfields[i - 1]] = key;

			//display the option 
			option_textfields[i - 1].text = option_components [1].Trim (); 
		}

		//hide options that aren't used 
		for (int i = 0; i < option_textfields.Length; i++) {
			if (i > current_coversation_breakdown.Count - 2) {
				option_textfields [i].transform.parent.gameObject.SetActive (false);
			} else {
				option_textfields [i].transform.parent.gameObject.SetActive (true);
			}
		}
	}

	private IEnumerator ChangeSprite (Sprite s, Image img) {
		RectTransform rect = img.GetComponent <RectTransform> ();
		float direction = rect.anchoredPosition.x / Mathf.Abs (rect.anchoredPosition.x);
		float width = img.GetComponentInParent<CanvasScaler> ().referenceResolution.x;
		float transition_time = 45f;

		if (s == img.sprite || (s == null && img.sprite == null)) { //nothing happens
			yield break;
		} else if (s != null && img.sprite == null) { //enter character
			img.sprite = s;
			rect.anchoredPosition += direction * new Vector2 (width, 0f);
			for (float i = 0; i <= transition_time; i++) {
				rect.anchoredPosition -= new Vector2 (width * direction / transition_time, 0f);
				img.color = new Color (1f, 1f, 1f, i / transition_time);
				yield return null;
			}
		} else if (s == null && img.sprite != null) { //exit character
			for (float i = 0; i <= transition_time; i++) {
				rect.anchoredPosition += new Vector2 (width * direction / transition_time, 0f);
				img.color = new Color (1f, 1f, 1f, (transition_time - i) / transition_time);
				yield return null;
			}
			rect.anchoredPosition -= direction * new Vector2 (width, 0f);
			img.sprite = null; 
		} else if (s.name.Split ('_')[0] != img.sprite.name.Split ('_')[0]) { //switch character
			for (float i = 0; i <= transition_time; i++) {
				rect.anchoredPosition += new Vector2 (width * direction / transition_time, 0f);
				img.color = new Color (1f, 1f, 1f, (transition_time - i) / transition_time);
				yield return null;
			}
			img.sprite = s;
			for (float i = 0; i <= transition_time; i++) {
				rect.anchoredPosition -= new Vector2 (width * direction / transition_time, 0f);
				img.color = new Color (1f, 1f, 1f, i / transition_time);
				yield return null;
			}
		} else { //same character different expression
			img.sprite = s;
			yield break;
		}
	}

	private IEnumerator AppendText (string text) {
		int i = 0;
		isTyping = true; 
		while (i < text.Length) {
			if (text.Substring (i, 1) == "<") { //resolving display issues with unity rich text
				int j = 1;
				int k = 1;
				while (j > 0 || text.Substring (i + k - 1, 1) != ">") {
					if (text.Substring (i + k, 1) == "/") {
						j--;
					}
					k++;
				}
				content.text = content.text + text.Substring (i, k);
				i += k;
				goto done;
			}
			content.text = content.text + text.Substring (i, 1); //normal appending with no rich text
			i++;
			done: yield return null; 
		}
		isTyping = false; 
	}
}
