INSERT INTO public.workflowsteps
(process, "name", sequencenum, tool, permissions, datecreated, dateupdated, lastmodifiedby, archived, lastmodifiedorigin)
values 
('Render', 'Transcribe',		1, '{"tool": "transcribe", "settings": ""}', '{}', current_timestamp, current_timestamp, (select id from users where email = 'sara_hentzel@sil.org'), false, 'sjh'),	
('Render', 'ParatextSync',		2, '{"tool": "paratext", "settings": ""}', '{}', current_timestamp, current_timestamp, (select id from users where email = 'sara_hentzel@sil.org'),false, 'sjh'),	
('Render', 'WholeBackTranslation',		3, '{"tool": "wholeBackTranslate", "settings": ""}', '{}', current_timestamp, current_timestamp,(select id from users where email = 'sara_hentzel@sil.org'), false, 'sjh'),	
('Render', 'WBTTranscribe',		4, '{"tool": "transcribe", "settings": "{\"artifactTypeId\":\"12\"}"}', '{}', current_timestamp, current_timestamp, (select id from users where email = 'sara_hentzel@sil.org'),false, 'sjh'),	
('Render', 'WBTParatextSync',		5, '{"tool": "paratext", "settings": "{\"artifactTypeId\":\"12\"}"}', '{}', current_timestamp, current_timestamp, (select id from users where email = 'sara_hentzel@sil.org'),false, 'sjh'),	
('Render', 'PhraseBackTranslation',		6, '{"tool": "phraseBackTranslate", "settings": ""}', '{}', current_timestamp, current_timestamp, (select id from users where email = 'sara_hentzel@sil.org'), false, 'sjh'),	
('Render', 'PBTTranscribe',		7, '{"tool": "transcribe", "settings": "{\"artifactTypeId\":\"4\"}"}', '{}', current_timestamp, current_timestamp, (select id from users where email = 'sara_hentzel@sil.org'),false, 'sjh'),	
('Render', 'PBTParatextSync',		8, '{"tool": "paratext", "settings": "{\"artifactTypeId\":\"4\"}"}', '{}', current_timestamp, current_timestamp,  (select id from users where email = 'sara_hentzel@sil.org'),false, 'sjh'),
('Render', 'Done',		9, '{"tool": "done"}', '{}', current_timestamp, current_timestamp,  (select id from users where email = 'sara_hentzel@sil.org'),false, 'sjh')




