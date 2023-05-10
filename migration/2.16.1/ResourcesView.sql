drop view public.resources;
CREATE OR REPLACE VIEW public.resources
AS WITH maxv AS (
         SELECT max(mm.versionnumber) AS versionnumber,
            mm.passageid
           FROM mediafiles mm
             JOIN plans ON mm.planid = plans.id
             JOIN projects ON plans.projectid = projects.id
          WHERE projects.ispublic = true AND mm.readytoshare
          GROUP BY mm.passageid
        ), latest AS (
         SELECT lm.id,
            true AS latest
           FROM mediafiles lm
             JOIN maxv ON lm.passageid = maxv.passageid AND lm.versionnumber = maxv.versionnumber
        )
 SELECT p.id,
    pr.id AS projectid,
    pr.name AS projectname,
    pr.organizationid,
    o.name AS organization,
    pr.language,
    pl.id AS planid,
    pl.name AS planname,
    pt.name AS plantype,
    s.id AS sectionid,
    s.name AS sectionname,
    s.sequencenum AS sectionsequencenum,
    m.id as mediafileid,
    p.id AS passageid,
    p.sequencenum AS passagesequencenum,
    p.book,
    p.reference,
    concat(s.sequencenum::character varying, '.', p.sequencenum::character varying, ' ',
        CASE
            WHEN p.book IS NULL THEN ''::text
            ELSE concat(p.book, ' ')
        END, p.reference) AS passagedesc,
    m.versionnumber,
    m.audiourl,
    m.duration,
    m.contenttype,
    m.transcription,
    m.originalfile,
    m.filesize,
    m.s3file,
    coalesce(c2.categoryname , c.categoryname) as categoryname,
    t.typename,
    m.lastmodifiedby,
    m.datecreated,
    m.dateupdated,
    m.lastmodifiedorigin,
    COALESCE(latest.latest, false) AS latest,
    s2.id as resourceid,
    s2.clusterid,
    s2.title,
    s2.description,
    coalesce(s2.languagebcp47, m.languagebcp47) as languagebcp47,
    s2.termsofuse,
    s2.keywords,
    s2.artifactcategoryid
   FROM organizations o
     JOIN projects pr ON pr.organizationid = o.id
     JOIN plans pl ON pl.projectid = pr.id
     JOIN plantypes pt ON pl.plantypeid = pt.id
     JOIN sections s ON s.planid = pl.id
     JOIN passages p ON p.sectionid = s.id
     JOIN mediafiles m ON p.id = m.passageid
     left join sharedresources s2 on p.id = s2.passageid
     LEFT JOIN artifactcategorys c ON m.artifactcategoryid = c.id
     LEFT JOIN artifactcategorys c2 ON s2.artifactcategoryid = c2.id
     LEFT JOIN artifacttypes t ON m.artifacttypeid = t.id
     LEFT JOIN latest ON m.id = latest.id
  WHERE pr.ispublic AND m.readytoshare AND NOT m.archived;

grant all on resources to transcriber;

