(async function(){
  if(!window.signalR){ console.error('SignalR client not loaded'); return; }
  const listContainerId = 'elicitation-requests';
  function ensureContainer(){
    let c = document.getElementById(listContainerId);
    if(!c){
      c = document.createElement('div');
      c.id = listContainerId;
      c.className = 'mt-4';
      c.innerHTML = '<h5>Live Elicitation Requests</h5><ul class="list-group" id="elicitation-list"></ul>';
      // place directly after the first POST form (Send Prompt form)
      const promptForm = document.querySelector('form[method="post"]');
      if(promptForm){
        promptForm.insertAdjacentElement('afterend', c);
      } else {
        // fallback: append to main
        document.querySelector('main')?.appendChild(c);
      }
    }
    return c;
  }

  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/elicitation')
    .withAutomaticReconnect()
    .build();

  connection.on('ElicitationPending', (payload)=>{
    ensureContainer();
    const ul = document.getElementById('elicitation-list');
    if(!ul) return;
    const li = document.createElement('li');
    li.className = 'list-group-item';
    li.id = `elic-${payload.id}`;
    li.innerHTML = `<div><strong>Request:</strong> ${payload.message || '(no message)'}</div>
      <div class="mt-2">
        <button class="btn btn-sm btn-success me-2" data-action="approve">Approve</button>
        <button class="btn btn-sm btn-danger" data-action="decline">Decline</button>
      </div>`;
    li.querySelectorAll('button').forEach(btn=>{
      btn.addEventListener('click', async ()=>{
        const act = btn.getAttribute('data-action');
        btn.disabled = true;
        try{
          await connection.invoke(act === 'approve' ? 'Approve' : 'Decline', payload.id);
        }catch(e){ console.error(e); }
      });
    });
    ul.appendChild(li);
  });

  connection.on('ElicitationCompleted', (payload)=>{
    const li = document.getElementById(`elic-${payload.id}`);
    if(li){
      li.classList.add(payload.approved ? 'list-group-item-success' : 'list-group-item-danger');
      li.querySelectorAll('button').forEach(b=> b.remove());
      const status = document.createElement('div');
      status.className = 'mt-2 fw-bold';
      status.textContent = payload.approved ? 'Approved' : 'Declined';
      li.appendChild(status);
    }
  });

  try{
    await connection.start();
  }catch(e){ console.error('SignalR start failed', e); }
})();
