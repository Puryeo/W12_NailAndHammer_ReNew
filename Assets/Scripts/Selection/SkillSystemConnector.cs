// SkillSystemConnector.cs - 연결 담당
using UnityEngine;

public class SkillSystemConnector : MonoBehaviour
{
    public static SkillSystemConnector Instance;

    private ISkillReceiver skillReceiver; // 팀원이 만든 스킬 매니저

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        // 팀원이 만든 스킬 매니저 찾기
        skillReceiver = FindObjectOfType<PlayerSkillManager>() as ISkillReceiver;

        if (skillReceiver == null)
        {
            Debug.LogError("스킬 매니저를 찾을 수 없습니다!");
        }
    }

    public void SendSkillToPlayer(SkillData skill)
    {
        skillReceiver?.ApplySkill(skill);
    }
}