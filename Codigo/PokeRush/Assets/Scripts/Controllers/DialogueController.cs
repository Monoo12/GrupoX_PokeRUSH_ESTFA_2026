using UnityEngine;

public class DialogueController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private DialogueView dialogueView;

    private StoryModel _storyModel;
    private DialogueDefinition _currentDialogue;
    private int _currentLineIndex;
    private System.Action _onDialogueComplete;

    public void StartDialogue(DialogueDefinition dialogue, StoryModel storyModel, System.Action onComplete = null)
    {
        if (dialogue == null) return;

        _currentDialogue = dialogue;
        _storyModel = storyModel;
        _currentLineIndex = 0;
        _onDialogueComplete = onComplete;

        dialogueView.Show();
        ShowCurrentLine();
    }

    public void AdvanceLine()
    {
        _currentLineIndex++;

        if (_currentLineIndex >= _currentDialogue.lines.Length)
        {
            FinishDialogue();
            return;
        }

        ShowCurrentLine();
    }

    private void ShowCurrentLine()
    {
        dialogueView.DisplayLine(
            _currentDialogue.speakerName,
            _currentDialogue.speakerPortrait,
            _currentDialogue.lines[_currentLineIndex]
        );
    }

    private void FinishDialogue()
    {
        if (_currentDialogue.grantsFlag != StoryFlag.None)
            _storyModel.ActivateFlag(_currentDialogue.grantsFlag);

        dialogueView.Hide();
        _onDialogueComplete?.Invoke();
    }
}