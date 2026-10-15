from pathlib import Path
root=Path(__file__).resolve().parent.parent
source=(root/'KIAS_REVIEW/ReviewEvidence.cs').read_text(encoding='utf8')
source=source.replace('// These evidence tests assert the observed defect, not the desired fixed behavior.\n','')
source=source.replace('KiasIndependentReviewEvidence','KiasReviewRegressionTests')
source=source.replace('RebuildLosesUnchangedBooleanOutput','RebuildPreservesUnchangedBooleanOutput')
source=source.replace('DataDisconnectionLeavesOpticalCameraActive','DataDisconnectionDeactivatesOpticalCamera')
source=source.replace('DirtyFirstGridBlocksSecondGridEvent','DirtyFirstGridDoesNotBlockSecondGridEvent')
source=source.replace('TestPulseCanActuateAfterFiveSecondTestExpires','TestPulseCannotActuateAfterFiveSecondTestExpires')
source=source.replace('out _, out _), Is.False,\n                "Evidence: rebuilding an unchanged active grid deletes the snapshot."','out var after, out _), Is.True,\n                "An unchanged connected output must survive a rebuild."')
source=source.replace('Assert.That(em.GetComponent<SurveillanceCameraComponent>(scanner).Active, Is.True,\n                "Evidence: the offline scanner still exposes an active native camera.");','Assert.That(em.GetComponent<SurveillanceCameraComponent>(scanner).Active, Is.False);')
source=source.replace('runtime.LastValue(cardB, 2, "Value").Number, Is.Zero,\n                "Evidence: a ready unrelated ship cannot consume its queued event."','runtime.LastValue(cardB, 2, "Value").Number, Is.EqualTo(1),\n                "A ready ship must continue while another topology is pending."')
source=source.replace('em.GetComponent<KiasGridComponent>(map.Grid).Testing','em.System<KiasServiceSystem>().IsTesting(map.Grid)')
source=source.replace('em.GetComponent<KiasDeviceAdapterComponent>(adapter).State, Is.True,\n                "Evidence: a diagnostic pulse caused a real delayed action."','em.GetComponent<KiasDeviceAdapterComponent>(adapter).State, Is.False,\n                "Diagnostic work must never reach the live actuator."')
(root/'Content.IntegrationTests/Tests/_Forge/KIAS/KiasReviewRegressionTests.cs').write_text(source,encoding='utf8')
for name in ['KiasGridTests.cs','KiasServiceTests.cs']:
    p=root/'Content.IntegrationTests/Tests/_Forge/KIAS'/name
    s=p.read_text(encoding='utf8').replace('em.GetComponent<KiasGridComponent>(map.Grid).Testing','em.System<KiasServiceSystem>().IsTesting(map.Grid)')
    if name=='KiasServiceTests.cs':
        s=s.replace('Alert, Is.EqualTo(KiasAlert.Contact)','Alert, Is.EqualTo(KiasAlert.Emergency)')
        s=s.replace('Cooldowns[100], Is.EqualTo(TimeSpan.FromSeconds(100))','Cooldowns[100], Is.EqualTo(TimeSpan.FromSeconds(200))')
    p.write_text(s,encoding='utf8')
p=root/'Content.Server/_Forge/KIAS/Controllers/KiasControllerUiSystem.cs'
s=p.read_text(encoding='utf8')
start=s.index('    public bool Edit(');end=s.index('    private void OnRackOpen',start)
body=s[start:end].replace('public bool Edit(', 'private bool EditDraft(').replace('Refresh(ent); ', '').replace('Refresh(ent);','')
wrapper='''    public bool Edit(Entity<KiasControllerProgrammerComponent> ent, EntityUid actor, KiasControllerEditMessage message)
    {
        var accepted = EditDraft(ent, actor, message);
        Refresh(ent);
        return accepted;
    }

'''
s=s[:start]+wrapper+body+s[end:]
p.write_text(s,encoding='utf8')
