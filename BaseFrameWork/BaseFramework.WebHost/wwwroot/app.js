async function run() {
  const root = document.getElementById('inspector');
  const response = await fetch('/api/preview');
  const data = await response.json();

  for (const member of data.members) {
    if (member.kind === 'method') {
      root.appendChild(renderMethod(member));
      continue;
    }

    const row = document.createElement('div');
    row.className = 'row';

    const label = document.createElement('label');
    label.textContent = member.displayName;
    row.appendChild(label);

    row.appendChild(renderInput(member));
    row.appendChild(renderStepButtons(member));
    root.appendChild(row);
  }
}

function renderInput(member) {
  if (member.kind === 'boolean') {
    const wrap = document.createElement('div');
    wrap.className = 'inline';
    const box = document.createElement('input');
    box.type = 'checkbox';
    box.checked = !!member.value;
    wrap.appendChild(box);
    return wrap;
  }

  if (member.kind === 'enum') {
    const select = document.createElement('select');
    for (const item of member.options) {
      const option = document.createElement('option');
      option.value = item;
      option.textContent = item;
      option.selected = item === member.value;
      select.appendChild(option);
    }
    return select;
  }

  const input = document.createElement('input');
  input.type = 'text';
  input.value = member.value;
  return input;
}

function renderStepButtons(member) {
  const canStep = member.kind === 'integer' || member.kind === 'double';
  const host = document.createElement('div');
  host.className = 'inline';
  if (!canStep) return host;
  const plus = document.createElement('button');
  plus.textContent = `+${member.step}`;
  const minus = document.createElement('button');
  minus.textContent = `-${member.step}`;
  host.append(plus, minus);
  return host;
}

function renderMethod(method) {
  const box = document.createElement('div');
  box.className = 'method';
  const tag = document.createElement('span');
  tag.className = 'pill';
  tag.textContent = 'Method';
  box.appendChild(tag);

  const button = document.createElement('button');
  button.textContent = method.displayName;
  box.appendChild(button);

  const grid = document.createElement('div');
  grid.className = 'method-grid';
  for (const param of method.parameters) {
    const label = document.createElement('label');
    label.textContent = param.name;
    grid.appendChild(label);
    grid.appendChild(renderInput(param));
  }

  box.appendChild(grid);
  return box;
}

run();
