using UnityEngine;

[CreateAssetMenu(fileName = "New Skill", menuName = "Skills/Skill")]
public class SkillData : ScriptableObject
{
    public int skillID;
    public string skillName;
    public Sprite skillIcon;
    public string description;
}